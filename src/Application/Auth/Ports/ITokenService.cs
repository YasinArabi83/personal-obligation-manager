namespace POM.Auth.Ports;

/// <summary>
/// The pair of tokens issued at login / refresh: a short-lived access JWT and a hashed, rotated,
/// revocable refresh token (docs/SECURITY.md §2, plan 0004).
/// </summary>
/// <param name="AccessToken">Short-lived (default 15 min) bearer JWT.</param>
/// <param name="ExpiresAt">Access-token expiry (UTC).</param>
/// <param name="RefreshToken">Opaque, URL-safe refresh token (raw — hashed only at rest). Default 30-day lifetime.</param>
public sealed record AuthTokens(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);

/// <summary>
/// Issues and rotates the access/refresh token pair. Access JWTs are signed with the configured
/// symmetric key; refresh tokens are random URL-safe bytes stored only as a SHA-256 hash, grouped by
/// a <c>family_id</c> so a replayed (already-consumed) token revokes the whole family (plan 0004 Q1-a).
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Fresh login: issues a new access JWT and a new refresh token in a <b>new</b> family for the user.
    /// </summary>
    Task<AuthTokens> IssueAsync(Guid userId, string phoneNumber, string? displayName, CancellationToken ct);

    /// <summary>
    /// Rotates a refresh token: validates it, revokes the consumed token, and issues a new access +
    /// refresh token in the <b>same</b> family. Returns <c>null</c> if the token is unknown, expired,
    /// or already revoked — and on a replay (re-use of a consumed token) <b>revokes the entire family</b>
    /// before rejecting.
    /// </summary>
    Task<AuthTokens?> TryRotateAsync(string refreshToken, CancellationToken ct);
}
