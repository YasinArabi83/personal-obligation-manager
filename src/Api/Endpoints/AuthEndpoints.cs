using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using POM.Auth;
using POM.Auth.Ports;

namespace POM.Endpoints;

// --- DTOs (API layer only — never expose entities/aggregates; AGENTS.md §5) ---

public sealed record OtpRequest([property: Required] string PhoneNumber);
public sealed record OtpVerify([property: Required] string PhoneNumber, [property: Required] string Code);
public sealed record RefreshRequest([property: Required] string RefreshToken);

public sealed record AuthUserDto(Guid Id, string PhoneNumber, string? DisplayName);
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, AuthUserDto User);
public sealed record RefreshResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);

/// <summary>
/// Minimal-API registration for the three auth routes (docs/API.md §2): <c>otp/request</c>,
/// <c>otp/verify</c>, <c>refresh</c>. All are <c>AllowAnonymous</c> (no JWT yet) and gated by the
/// coarse per-IP rate limiter + the per-phone <see cref="IOtpRateLimiter"/>. Token rotation on
/// <c>refresh</c> is handled directly by <see cref="ITokenService"/> (plan 0004).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .AllowAnonymous()
            .RequireRateLimiting(AuthIpPolicyName)
            .WithTags("Auth");

        group.MapPost("/otp/request", RequestOtpAsync);
        group.MapPost("/otp/verify", VerifyOtpAsync);
        group.MapPost("/refresh", RefreshAsync);

        return app;
    }

    private static async Task<IResult> RequestOtpAsync(
        [FromBody] OtpRequest request,
        AuthAppService auth,
        HttpContext http,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.PhoneNumber))
            return AuthErrors.Validation("phoneNumber is required.");

        var result = await auth.RequestOtpAsync(request.PhoneNumber, ct);
        if (result is OtpRequestResult.RateLimited rl)
        {
            SetRetryAfter(http, rl.RetryAfter);
            return AuthErrors.OtpRateLimited();
        }
        return Results.NoContent();
    }

    private static async Task<IResult> VerifyOtpAsync(
        [FromBody] OtpVerify request,
        AuthAppService auth,
        HttpContext http,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.PhoneNumber) || string.IsNullOrWhiteSpace(request?.Code))
            return AuthErrors.Validation("phoneNumber and code are required.");

        var result = await auth.VerifyOtpAsync(request.PhoneNumber, request.Code, ct);
        return result switch
        {
            OtpVerifyResult.Success s => Results.Ok(new AuthResponse(
                s.Tokens.AccessToken, s.Tokens.ExpiresAt, s.Tokens.RefreshToken,
                new AuthUserDto(s.User.Id, s.User.PhoneNumber, s.User.DisplayName))),
            OtpVerifyResult.RateLimited rl => RateLimited(http, rl.RetryAfter, AuthErrors.OtpRateLimited()),
            _ => AuthErrors.OtpInvalid(),
        };
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        ITokenService tokens,
        HttpContext http,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
            return AuthErrors.Validation("refreshToken is required.");

        var rotated = await tokens.TryRotateAsync(request.RefreshToken, ct);
        if (rotated is null)
            return AuthErrors.Unauthorized("Invalid or expired refresh token.");

        return Results.Ok(new RefreshResponse(rotated.AccessToken, rotated.ExpiresAt, rotated.RefreshToken));
    }

    private const string AuthIpPolicyName = "auth-ip";

    private static void SetRetryAfter(HttpContext http, TimeSpan? retry)
    {
        if (retry is { } r)
            http.Response.Headers["Retry-After"] = ((int)Math.Ceiling(r.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    }

    private static IResult RateLimited(HttpContext http, TimeSpan retry, IResult result)
    {
        SetRetryAfter(http, retry);
        return result;
    }

    private static class AuthErrors
    {
        public static IResult Validation(string message) =>
            Results.Json(new { error = new { code = "validation_error", message } }, statusCode: 400);

        public static IResult OtpInvalid() =>
            Results.Json(new { error = new { code = "otp_invalid_or_expired", message = "The code is invalid or expired." } }, statusCode: 401);

        public static IResult OtpRateLimited() =>
            Results.Json(new { error = new { code = "otp_rate_limited", message = "Too many attempts. Try again later." } }, statusCode: 429);

        public static IResult Unauthorized(string message) =>
            Results.Json(new { error = new { code = "unauthorized", message } }, statusCode: 401);
    }
}
