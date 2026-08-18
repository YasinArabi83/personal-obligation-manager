using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POM.Auth.Ports;
using POM.Auth.RateLimiting;
using POM.Identity;
using POM.Sms;

namespace POM.Auth.RateLimiting;

/// <summary>
/// Registers the Application-port implementations for the OTP auth flow (SMS sender, OTP sender/verifier,
/// token service, per-phone rate limiter) plus the <see cref="AuthAppService"/> orchestration. Named
/// <c>AddOtpAuthInfrastructure</c> to avoid colliding with the Identity <c>AddAuth</c> extension from
/// task 0003 (plan 0004 Q1-naming); the API composition root calls both. JWT bearer validation and the
/// per-IP rate-limit middleware are wired in the API layer (composition root) where the ASP.NET Core
/// shared framework lives.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddOtpAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TimeProvider is shared by the token service and the rate limiter so expiry/windows are testable.
        services.AddSingleton(TimeProvider.System);

        // SMS — only the dev logging sender exists for now; the real Iranian provider (sms.ir) lands in 0017.
        var smsProvider = configuration["Sms:Provider"] ?? "Logging";
        if (smsProvider != "Logging")
        {
            throw new InvalidOperationException(
                $"Unknown Sms:Provider '{smsProvider}'. Only 'Logging' is implemented (real provider is task 0017).");
        }
        services.AddSingleton<ISmsSender, LoggingSmsSender>();

        services.AddScoped<IOtpSender, OtpSender>();
        services.AddScoped<IOtpVerifier, OtpVerifier>();
        services.AddScoped<ITokenService, Jwt.JwtTokenService>();
        services.AddSingleton<IOtpRateLimiter, InMemoryOtpRateLimiter>();

        services.AddScoped<AuthAppService>();
        return services;
    }
}
