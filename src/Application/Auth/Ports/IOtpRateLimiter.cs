namespace POM.Auth.Ports;

/// <summary>
/// The outcome of an OTP rate-limit check: allowed, or denied with a hint of how long to wait.
/// </summary>
/// <param name="Allowed">Whether the request may proceed.</param>
/// <param name="RetryAfter">When denied, the approximate time until the limit resets (for the <c>Retry-After</c> header).</param>
public sealed record RateLimitDecision(bool Allowed, TimeSpan? RetryAfter);

/// <summary>
/// Per-phone-number OTP rate limiting (docs/SECURITY.md §2): max 3 OTP requests per phone per 10 min,
/// max 5 failed verifies per phone per 1 h. The built-in <c>AddRateLimiter</c> middleware handles the
/// coarse per-IP gate (it cannot partition on a JSON-body phone number); this enforces the per-phone
/// windows inside the application. In-memory, single-VPS MVP (no Redis) — plan 0004 Q2.
/// </summary>
public interface IOtpRateLimiter
{
    /// <summary>Checks (and records) an <c>otp/request</c> for <paramref name="phoneNumber"/>.</summary>
    Task<RateLimitDecision> CheckOtpRequestAsync(string phoneNumber, CancellationToken ct);

    /// <summary>Checks whether <paramref name="phoneNumber"/> may still attempt <c>otp/verify</c> (failed-count window).</summary>
    Task<RateLimitDecision> CheckOtpVerifyAsync(string phoneNumber, CancellationToken ct);

    /// <summary>Records a failed verify attempt for <paramref name="phoneNumber"/> (counts toward the verify window).</summary>
    Task RecordVerifyFailureAsync(string phoneNumber, CancellationToken ct);
}
