using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

namespace POM;

/// <summary>
/// API-layer composition: JWT bearer validation, authorization, and the coarse per-IP rate limiter
/// on the auth endpoints. This lives in the API layer (not Infrastructure) because these are ASP.NET
/// Core hosting/middleware concerns backed by the Web SDK shared framework. The symmetric signing key,
/// issuer, audience, and lifetimes are read from <c>Jwt:*</c> configuration and must match the token
/// service that mints the tokens (plan 0004).
/// </summary>
public static class ApiAuthExtensions
{
    /// <summary>Coarse per-IP limiter policy applied to <c>auth/*</c> routes (plan 0004 Q2).</summary>
    public const string AuthIpPolicy = "auth-ip";

    public static IServiceCollection AddPomApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes (256 bits).");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "pom",
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"] ?? "pom-web",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                };
            });

        services.AddAuthorization();

        // Per-IP coarse gate. The fine per-phone window (3 OTP/10 min, 5 failed verify/1 h) is enforced
        // inside the application by IOtpRateLimiter — the middleware cannot partition on a JSON-body phone.
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AuthIpPolicy, context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = false,
                    QueueLimit = 0,
                });
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
