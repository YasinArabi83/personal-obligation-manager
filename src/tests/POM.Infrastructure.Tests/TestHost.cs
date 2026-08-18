using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using POM.Auth;
using POM.Auth.Ports;
using POM.Auth.RateLimiting;
using POM.Identity.DependencyInjection;
using POM.Persistence;

namespace POM.Infrastructure.Tests;

/// <summary>
/// Builds a DI container with the full auth stack (DbContext + Identity + OTP auth infrastructure)
/// against a given Postgres connection string. A <see cref="FakeTimeProvider"/> is registered so
/// token-expiry and rate-limit window tests can advance the clock deterministically.
/// </summary>
internal static class TestHost
{
    public const string TestSigningKey = "test-signing-key-at-least-32-bytes-long-for-hs256!";

    public static ServiceProvider Build(string connectionString, FakeTimeProvider? clock = null)
    {
        var time = clock ?? new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        var services = new ServiceCollection();
        services.AddDbContext<PomDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            PomDbContext.ConfigureOptions(options);
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:Issuer"] = "pom",
                ["Jwt:Audience"] = "pom-web",
                ["Jwt:AccessLifetimeMinutes"] = "15",
                ["Jwt:RefreshLifetimeDays"] = "30",
                ["Sms:Provider"] = "Logging",
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        services.AddAuth(config);
        services.AddOtpAuthInfrastructure(config);

        // Replace the real TimeProvider (registered by AddOtpAuthInfrastructure) with the fake clock
        // so JwtTokenService / InMemoryOtpRateLimiter share the controllable clock.
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(time);

        return services.BuildServiceProvider();
    }
}
