using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POM.Persistence;

namespace POM.Identity.DependencyInjection;

/// <summary>
/// The single Identity entry point the API composition root calls (after
/// <c>AddInfrastructure</c>). Hosts Identity's token machinery only — <c>AddIdentityCore</c> plus
/// the <b>built-in</b> <see cref="PhoneNumberTokenProvider{TUser}"/> registered under
/// <see cref="TokenOptions.DefaultPhoneProviderProviderKey"/>. No custom OTP token-provider class
/// is written (ADR-0016, Q2); Identity's framework-managed ~3–6 minute TOTP window is accepted as-is.
/// All store plumbing is the custom <see cref="UserStore"/> over the clean <c>users</c> table, so
/// no password column and no <c>AspNet*</c> tables are ever created.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddIdentityCore<ApplicationUser>(options =>
        {
            // We carry no password, but keep lockout/password options sane/disabled explicitly.
            options.Lockout.AllowedForNewUsers = false;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 0;
            options.User.RequireUniqueEmail = false;
            options.User.AllowedUserNameCharacters = string.Empty; // phone numbers only; digits/+ etc.
        });

        // Built-in phone token provider under the framework's default phone-provider key ("Phone").
        builder.AddTokenProvider<PhoneNumberTokenProvider<ApplicationUser>>(TokenOptions.DefaultPhoneProvider);

        // Custom store over PomDbContext.Users (clean `users` table).
        services.AddScoped<IUserStore<ApplicationUser>, UserStore>();

        return services;
    }
}
