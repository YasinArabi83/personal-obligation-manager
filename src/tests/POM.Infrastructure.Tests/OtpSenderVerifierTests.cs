using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POM.Auth.Ports;
using POM.Persistence;

namespace POM.Infrastructure.Tests;

/// <summary>
/// Find-or-create + verify behavior for the OTP flow (plan 0004). Per ADR-0017, the user row is
/// ensured at <b>request</b> time (OtpSender) so the token's entropy is persisted; a successful
/// verify confirms the phone number. These run against real Postgres + the real Identity token machinery.
/// </summary>
public sealed class OtpSenderVerifierTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public OtpSenderVerifierTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<ServiceProvider> BuildHostAsync()
    {
        var provider = TestHost.Build(_fixture.ConnectionString);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PomDbContext>().Database.MigrateAsync();
        return provider;
    }

    [Fact]
    public async Task OtpSender_creates_user_on_first_request_and_returns_code()
    {
        using var provider = await BuildHostAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var sender = sp.GetRequiredService<IOtpSender>();
        var db = sp.GetRequiredService<PomDbContext>();

        var phone = "+9892000001";
        var code = await sender.SendOtpAsync(phone, default);

        Assert.False(string.IsNullOrWhiteSpace(code));
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.PhoneNumber == phone);
        Assert.False(user.PhoneNumberConfirmed); // not confirmed until verified
    }

    [Fact]
    public async Task OtpSender_is_idempotent_for_existing_user()
    {
        using var provider = await BuildHostAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var sender = sp.GetRequiredService<IOtpSender>();
        var db = sp.GetRequiredService<PomDbContext>();

        var phone = "+9892000002";
        await sender.SendOtpAsync(phone, default);
        await sender.SendOtpAsync(phone, default);

        var count = await db.Users.AsNoTracking().CountAsync(u => u.PhoneNumber == phone);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Verifier_confirms_phone_on_correct_code_and_rejects_wrong()
    {
        using var provider = await BuildHostAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var sender = sp.GetRequiredService<IOtpSender>();
        var verifier = sp.GetRequiredService<IOtpVerifier>();
        var db = sp.GetRequiredService<PomDbContext>();

        var phone = "+9892000003";
        var code = await sender.SendOtpAsync(phone, default);

        var verified = await verifier.VerifyAsync(phone, code, default);
        Assert.NotNull(verified);
        Assert.Equal(phone, verified!.PhoneNumber);

        var confirmed = await db.Users.AsNoTracking().SingleAsync(u => u.PhoneNumber == phone);
        Assert.True(confirmed.PhoneNumberConfirmed);

        // A wrong code for a fresh user fails.
        var phone2 = "+9892000004";
        await sender.SendOtpAsync(phone2, default);
        var bad = await verifier.VerifyAsync(phone2, "000000", default);
        Assert.Null(bad);
    }
}
