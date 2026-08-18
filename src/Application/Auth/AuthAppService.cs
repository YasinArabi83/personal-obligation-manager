using POM.Auth.Ports;

namespace POM.Auth;

/// <summary>Outcome of <c>POST auth/otp/request</c>.</summary>
public abstract record OtpRequestResult
{
    /// <summary>The OTP was generated and dispatched (→ HTTP 204).</summary>
    public sealed record Sent : OtpRequestResult;

    /// <summary>Too many OTP requests for this phone (→ HTTP 429).</summary>
    public sealed record RateLimited(TimeSpan RetryAfter) : OtpRequestResult;
}

/// <summary>Outcome of <c>POST auth/otp/verify</c>.</summary>
public abstract record OtpVerifyResult
{
    /// <summary>The code verified; fresh tokens were issued (→ HTTP 200).</summary>
    public sealed record Success(AuthTokens Tokens, VerifiedUser User) : OtpVerifyResult;

    /// <summary>The code was invalid or expired (→ HTTP 401).</summary>
    public sealed record Invalid : OtpVerifyResult;

    /// <summary>Too many failed verifies for this phone (→ HTTP 429).</summary>
    public sealed record RateLimited(TimeSpan RetryAfter) : OtpVerifyResult;
}

/// <summary>
/// Thin orchestration of the OTP login flow. Holds no business logic of its own: it sequences the
/// ports — rate-check → send (request), and rate-check → verify → issue tokens → record-failure
/// (verify). Token rotation for <c>auth/refresh</c> is handled directly by the endpoint calling
/// <see cref="ITokenService"/> (plan 0004). Per-phone rate limiting lives in <see cref="IOtpRateLimiter"/>
/// (the built-in middleware only gates per-IP — plan 0004 Q2).
/// </summary>
public sealed class AuthAppService
{
    private readonly IOtpSender _otpSender;
    private readonly IOtpVerifier _otpVerifier;
    private readonly ITokenService _tokenService;
    private readonly IOtpRateLimiter _rateLimiter;

    public AuthAppService(
        IOtpSender otpSender,
        IOtpVerifier otpVerifier,
        ITokenService tokenService,
        IOtpRateLimiter rateLimiter)
    {
        _otpSender = otpSender;
        _otpVerifier = otpVerifier;
        _tokenService = tokenService;
        _rateLimiter = rateLimiter;
    }

    public async Task<OtpRequestResult> RequestOtpAsync(string phoneNumber, CancellationToken ct)
    {
        var decision = await _rateLimiter.CheckOtpRequestAsync(phoneNumber, ct);
        if (!decision.Allowed)
            return new OtpRequestResult.RateLimited(decision.RetryAfter ?? TimeSpan.FromMinutes(10));

        await _otpSender.SendOtpAsync(phoneNumber, ct);
        return new OtpRequestResult.Sent();
    }

    public async Task<OtpVerifyResult> VerifyOtpAsync(string phoneNumber, string code, CancellationToken ct)
    {
        var decision = await _rateLimiter.CheckOtpVerifyAsync(phoneNumber, ct);
        if (!decision.Allowed)
            return new OtpVerifyResult.RateLimited(decision.RetryAfter ?? TimeSpan.FromHours(1));

        var user = await _otpVerifier.VerifyAsync(phoneNumber, code, ct);
        if (user is null)
        {
            // Count this failure toward the verify lockout window, then reject.
            await _rateLimiter.RecordVerifyFailureAsync(phoneNumber, ct);
            return new OtpVerifyResult.Invalid();
        }

        var tokens = await _tokenService.IssueAsync(user.Id, user.PhoneNumber, user.DisplayName, ct);
        return new OtpVerifyResult.Success(tokens, user);
    }
}
