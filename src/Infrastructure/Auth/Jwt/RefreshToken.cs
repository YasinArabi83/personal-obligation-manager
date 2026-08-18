using POM.Persistence;

namespace POM.Auth.Jwt;

/// <summary>
/// Infrastructure persistence model for a refresh token (NOT a Domain aggregate — a refresh token is
/// an auth/infra concern, not domain behavior; plan 0004 Q3). Stored on the <c>refresh_tokens</c>
/// table; the raw token is never stored — only its SHA-256 <see cref="TokenHash"/>. Tokens are
/// grouped by <see cref="FamilyId"/>: all tokens minted at one login share a family, so a replayed
/// (already-consumed) token triggers family-wide revocation (plan 0004 Q1-a).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }

    /// <summary>SHA-256 of the raw token (32 bytes). The raw token is returned to the client only.</summary>
    public byte[] TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the token is consumed (rotated) or revoked. Null while active.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
