using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using POM.Auth.Ports;
using POM.Persistence;
using POM.Users;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace POM.Auth.Jwt;

/// <summary>
/// <see cref="ITokenService"/> over signed JWTs (access) and hashed, rotated, revocable refresh
/// tokens (docs/SECURITY.md §2, plan 0004). The signing key, issuer, audience, and lifetimes are read
/// from configuration (<c>Jwt:*</c>) — never hardcoded. Refresh tokens are random URL-safe bytes
/// stored only as a SHA-256 hash and grouped by <see cref="RefreshToken.FamilyId"/>; replaying a
/// consumed token revokes the whole family (plan 0004 Q1-a).
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private const int AccessTokenDefaultMinutes = 15;
    private const int RefreshTokenDefaultDays = 30;

    private readonly PomDbContext _db;
    private readonly TimeProvider _time;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessLifetimeMinutes;
    private readonly int _refreshLifetimeDays;

    public JwtTokenService(PomDbContext db, IConfiguration configuration, TimeProvider time)
    {
        _db = db;
        _time = time;

        var key = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var rawKey = Encoding.UTF8.GetBytes(key);
        if (rawKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes (256 bits).");
        _signingKey = new SymmetricSecurityKey(rawKey);

        _issuer = configuration["Jwt:Issuer"] ?? "pom";
        _audience = configuration["Jwt:Audience"] ?? "pom-web";
        _accessLifetimeMinutes = configuration.GetValue<int?>("Jwt:AccessLifetimeMinutes") ?? AccessTokenDefaultMinutes;
        _refreshLifetimeDays = configuration.GetValue<int?>("Jwt:RefreshLifetimeDays") ?? RefreshTokenDefaultDays;
    }

    /// <inheritdoc/>
    public async Task<AuthTokens> IssueAsync(Guid userId, string phoneNumber, string? displayName, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var familyId = Guid.NewGuid();
        var refresh = NewRefreshToken(userId, familyId, now);

        await _db.RefreshTokens.AddAsync(refresh.entity, ct);
        await _db.SaveChangesAsync(ct);

        var accessExpiresAt = now.AddMinutes(_accessLifetimeMinutes);
        var accessToken = WriteAccessToken(userId, phoneNumber, displayName, accessExpiresAt);
        return new AuthTokens(accessToken, accessExpiresAt, refresh.rawToken);
    }

    /// <inheritdoc/>
    public async Task<AuthTokens?> TryRotateAsync(string refreshToken, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var hash = HashToken(refreshToken);

        // Match the presented token by its hash (unique index guarantees at most one).
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (stored is null)
            return null; // unknown token

        if (stored.RevokedAt is not null)
        {
            // A previously-consumed (rotated) token is being replayed → revoke the whole family.
            await RevokeFamilyAsync(stored.FamilyId, now, ct);
            return null;
        }

        if (stored.ExpiresAt <= now)
        {
            // Naturally expired; revoke it and reject (do not implicate the family).
            stored.RevokedAt = now;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        // Valid: consume it (revoke) and issue a fresh token in the same family.
        stored.RevokedAt = now;
        var next = NewRefreshToken(stored.UserId, stored.FamilyId, now);
        await _db.RefreshTokens.AddAsync(next.entity, ct);
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        var accessExpiresAt = now.AddMinutes(_accessLifetimeMinutes);
        var accessToken = WriteAccessToken(stored.UserId, user?.PhoneNumber ?? string.Empty, user?.DisplayName, accessExpiresAt);
        return new AuthTokens(accessToken, accessExpiresAt, next.rawToken);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        var active = await _db.RefreshTokens
            .Where(r => r.FamilyId == familyId && r.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in active)
            token.RevokedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    private string WriteAccessToken(Guid userId, string phoneNumber, string? displayName, DateTimeOffset expiresAt)
    {
        var now = _time.GetUtcNow();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Name, phoneNumber),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
        };
        if (!string.IsNullOrWhiteSpace(displayName))
            claims.Add(new Claim("display_name", displayName));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
        };

        // JsonWebTokenHandler writes a compact JWT without the XML/WRAP overhead of the older JwtSecurityTokenHandler.
        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }

    private (RefreshToken entity, string rawToken) NewRefreshToken(Guid userId, Guid familyId, DateTimeOffset now)
    {
        // 32 cryptographically random bytes → URL-safe base64 (no padding) → ~43 chars.
        var bytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Base64UrlEncoder.Encode(bytes);

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = now.AddDays(_refreshLifetimeDays),
            CreatedAt = now,
        };
        return (entity, rawToken);
    }

    private static byte[] HashToken(string rawToken) => SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
}
