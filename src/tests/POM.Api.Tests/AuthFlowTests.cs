using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POM.Auth.Ports;
using POM.Persistence;

namespace POM.Api.Tests;

/// <summary>
/// End-to-end HTTP integration for the auth endpoints (plan 0004): OTP request + per-phone rate
/// limiting, the verify → refresh → rotate → replay flow, and that the per-phone rate-limit state is
/// isolated. Runs the real <c>Program</c> against a disposable Postgres (see <see cref="ApiFactory"/>).
/// </summary>
public sealed class AuthFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static int _phoneSeq;

    public AuthFlowTests(ApiFactory factory) => _factory = factory;

    private static string NextPhone() => $"+98930{(Interlocked.Increment(ref _phoneSeq)):D7}";

    private static JsonDocument Body(HttpResponseMessage r) => JsonDocument.Parse(r.Content.ReadAsStringAsync().Result);

    [Fact]
    public async Task Healthz_is_public()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Otp_request_returns_204_then_429_after_three_per_phone()
    {
        var client = _factory.CreateClient();
        var phone = NextPhone();
        var body = new { phoneNumber = phone };

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/otp/request", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/otp/request", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/otp/request", body)).StatusCode);

        var blocked = await client.PostAsJsonAsync("/api/v1/auth/otp/request", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_state_is_isolated_per_phone()
    {
        var client = _factory.CreateClient();
        var a = NextPhone();
        var b = NextPhone();

        // Exhaust phone A.
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = a });

        // Phone B is unaffected.
        var resp = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = b });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task Full_verify_refresh_rotate_replay_flow()
    {
        var client = _factory.CreateClient();
        var phone = NextPhone();

        // Drive an OTP request through HTTP (asserts the request path + 204).
        var req = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone });
        Assert.Equal(HttpStatusCode.NoContent, req.StatusCode);

        // Obtain the code via the SMS port (the dev LoggingSmsSender is the delivery channel) and verify over HTTP.
        string code;
        using (var scope = _factory.Services.CreateScope())
        {
            code = await scope.ServiceProvider.GetRequiredService<IOtpSender>().SendOtpAsync(phone, default);
        }

        var verify = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phoneNumber = phone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var verifyJson = Body(verify).RootElement;
        var accessToken = verifyJson.GetProperty("accessToken").GetString();
        var refreshToken = verifyJson.GetProperty("refreshToken").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));
        Assert.False(string.IsNullOrEmpty(refreshToken));
        Assert.Equal(phone, verifyJson.GetProperty("user").GetProperty("phoneNumber").GetString());

        // A wrong code for another phone fails with 401.
        var phone2 = NextPhone();
        using (var scope2 = _factory.Services.CreateScope())
        {
            await scope2.ServiceProvider.GetRequiredService<IOtpSender>().SendOtpAsync(phone2, default);
        }
        var bad = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phoneNumber = phone2, code = "000000" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        // Refresh → new tokens, consumed token revoked.
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshJson = Body(refresh).RootElement;
        var newRefresh = refreshJson.GetProperty("refreshToken").GetString()!;
        var newAccess = refreshJson.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrEmpty(newAccess));
        Assert.NotEqual(refreshToken, newRefresh);

        using (var scope3 = _factory.Services.CreateScope())
        {
            var db = scope3.ServiceProvider.GetRequiredService<PomDbContext>();
            var consumed = await db.RefreshTokens.AsNoTracking()
                .Where(r => r.UserId != Guid.Empty) // keep the query shape stable
                .CountAsync();
            Assert.True(consumed >= 2); // original + rotated exist
        }

        // Replay the old (consumed) refresh token → 401, and the rotated token is now dead too (family revoked).
        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var replayRotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = newRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, replayRotated.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_unknown_token_returns_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "does-not-exist" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
