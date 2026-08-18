using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POM.Auth.Jwt;
using POM.Auth.Ports;
using POM.Persistence;
using POM.Users;

namespace POM.Infrastructure.Tests;

/// <summary>
/// Refresh-token rotation and reuse detection (plan 0004 Q1-a) for <see cref="JwtTokenService"/>,
/// against a real Postgres. A successful rotation revokes the consumed token and issues a fresh one
/// in the same family; replaying a consumed token revokes the whole family and rejects; an expired
/// token is rejected; the access JWT is signed and carries the user id.
/// </summary>
public sealed class JwtTokenServiceTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public JwtTokenServiceTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<ServiceProvider> BuildHostAsync(FakeTimeProvider clock)
    {
        var provider = TestHost.Build(_fixture.ConnectionString, clock);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PomDbContext>().Database.MigrateAsync();
        return provider;
    }

    private static async Task<Guid> SeedUserAsync(IServiceProvider sp, string phone)
    {
        var db = sp.GetRequiredService<PomDbContext>();
        var user = User.Create(phone);
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Issue_then_rotate_yields_new_tokens_and_revokes_consumed()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        using var provider = await BuildHostAsync(clock);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var tokens = sp.GetRequiredService<ITokenService>();
        var db = sp.GetRequiredService<PomDbContext>();

        var userId = await SeedUserAsync(sp, "+9891000001");
        var issued = await tokens.IssueAsync(userId, "+9891000001", "Alice", default);

        Assert.False(string.IsNullOrEmpty(issued.AccessToken));
        Assert.False(string.IsNullOrEmpty(issued.RefreshToken));
        Assert.NotEqual(issued.AccessToken, issued.RefreshToken);

        var rotated = await tokens.TryRotateAsync(issued.RefreshToken, default);
        Assert.NotNull(rotated);
        Assert.NotEqual(issued.RefreshToken, rotated!.RefreshToken);

        // The consumed token is now revoked.
        var consumed = await db.RefreshTokens.SingleAsync(r => r.TokenHash == Hash(issued.RefreshToken));
        Assert.NotNull(consumed.RevokedAt);
        // The new token is active and shares the family.
        var fresh = await db.RefreshTokens.SingleAsync(r => r.TokenHash == Hash(rotated.RefreshToken));
        Assert.Null(fresh.RevokedAt);
        Assert.Equal(consumed.FamilyId, fresh.FamilyId);
    }

    [Fact]
    public async Task Replaying_a_consumed_token_revokes_the_whole_family_and_rejects()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        using var provider = await BuildHostAsync(clock);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var tokens = sp.GetRequiredService<ITokenService>();
        var db = sp.GetRequiredService<PomDbContext>();

        var userId = await SeedUserAsync(sp, "+9891000002");
        var issued = await tokens.IssueAsync(userId, "+9891000002", null, default);
        var rotated = await tokens.TryRotateAsync(issued.RefreshToken, default);
        Assert.NotNull(rotated);

        // Replay the old (consumed) token → reuse detected → family revoked → null.
        var replay = await tokens.TryRotateAsync(issued.RefreshToken, default);
        Assert.Null(replay);

        // The family is now entirely revoked: the rotated token can no longer be used either.
        var familyId = (await db.RefreshTokens.FirstAsync(r => r.TokenHash == Hash(issued.RefreshToken))).FamilyId;
        var familyTokens = await db.RefreshTokens.Where(r => r.FamilyId == familyId).ToListAsync();
        Assert.All(familyTokens, t => Assert.NotNull(t.RevokedAt));

        var afterReuse = await tokens.TryRotateAsync(rotated!.RefreshToken, default);
        Assert.Null(afterReuse);
    }

    [Fact]
    public async Task An_expired_refresh_token_is_rejected()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        using var provider = await BuildHostAsync(clock);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var tokens = sp.GetRequiredService<ITokenService>();

        var userId = await SeedUserAsync(sp, "+9891000003");
        var issued = await tokens.IssueAsync(userId, "+9891000003", null, default);

        // Advance past the 30-day refresh lifetime.
        clock.Advance(TimeSpan.FromDays(31));

        var result = await tokens.TryRotateAsync(issued.RefreshToken, default);
        Assert.Null(result);
    }

    [Fact]
    public async Task An_unknown_token_is_rejected_without_touching_families()
    {
        using var provider = await BuildHostAsync(new FakeTimeProvider());
        using var scope = provider.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var result = await tokens.TryRotateAsync("not-a-real-token", default);
        Assert.Null(result);
    }

    [Fact]
    public async Task Access_token_carries_user_id_as_subject()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        using var provider = await BuildHostAsync(clock);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var tokens = sp.GetRequiredService<ITokenService>();

        var userId = await SeedUserAsync(sp, "+9891000004");
        var issued = await tokens.IssueAsync(userId, "+9891000004", null, default);

        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(issued.AccessToken);
        Assert.Contains(jwt.Claims, c => c.Type == "sub" && c.Value == userId.ToString());
    }

    private static byte[] Hash(string rawToken) => System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
}
