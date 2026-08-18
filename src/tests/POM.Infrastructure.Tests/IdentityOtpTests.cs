using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POM.Identity;
using POM.Identity.DependencyInjection;
using POM.Persistence;

namespace POM.Infrastructure.Tests;

/// <summary>
/// End-to-end check that OTP generation/verification works at the service level through the real
/// ASP.NET Core Identity machinery (custom store + built-in <c>PhoneNumberTokenProvider</c>)
/// resolved from DI against a real Postgres. A correct token verifies; a tampered one does not.
/// The framework-managed ~3–6 minute validity window is not asserted as an exact value (it is not
/// configurable — ADR-0016, Q2).
/// </summary>
public sealed class IdentityOtpTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public IdentityOtpTests(PostgresFixture fixture) => _fixture = fixture;

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PomDbContext>(options =>
        {
            options.UseNpgsql(_fixture.ConnectionString);
            PomDbContext.ConfigureOptions(options);
        });
        // IConfiguration is required by AddAuth; an empty config is fine (no auth knobs configured).
        services.AddAuth(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Phone_token_can_be_generated_and_verified()
    {
        using var provider = BuildServices();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PomDbContext>();
        await db.Database.MigrateAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var phone = "+989121111111";

        var user = new ApplicationUser { UserName = phone, PhoneNumber = phone };
        var create = await userManager.CreateAsync(user);
        Assert.True(create.Succeeded);

        var token = await userManager.GenerateUserTokenAsync(
            user, TokenOptions.DefaultPhoneProvider, "PhoneNumber");

        Assert.False(string.IsNullOrEmpty(token));
        Assert.True(token!.Length >= 4 && token.All(char.IsDigit),
            $"Expected a numeric OTP, got '{token}'.");

        var valid = await userManager.VerifyUserTokenAsync(
            user, TokenOptions.DefaultPhoneProvider, "PhoneNumber", token);
        Assert.True(valid);
    }

    [Fact]
    public async Task A_wrong_code_does_not_verify()
    {
        using var provider = BuildServices();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PomDbContext>();
        await db.Database.MigrateAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var phone = "+989122222222";

        var user = new ApplicationUser { UserName = phone, PhoneNumber = phone };
        await userManager.CreateAsync(user);

        var token = await userManager.GenerateUserTokenAsync(
            user, TokenOptions.DefaultPhoneProvider, "PhoneNumber");

        // A different code must fail. (Could theoretically collide on a 6-digit space, but the
        // generated token and "000000" are vanishingly unlikely to be equal.)
        var wrong = new string(token!.Where(char.IsDigit).Select(_ => '0').DefaultIfEmpty('0').ToArray());
        if (wrong == token) wrong = "999999";

        var ok = await userManager.VerifyUserTokenAsync(
            user, TokenOptions.DefaultPhoneProvider, "PhoneNumber", wrong);
        Assert.False(ok);
    }

    [Fact]
    public async Task UserStore_persists_to_clean_users_table_only()
    {
        using var provider = BuildServices();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PomDbContext>();
        await db.Database.MigrateAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var phone = "+989123333333";
        var user = new ApplicationUser { UserName = phone, PhoneNumber = phone };
        await userManager.CreateAsync(user);

        // The created user lands on the clean `users` table (shared container across the class,
        // so filter to this test's phone) carrying the phone + a security stamp.
        var persisted = await db.Users.AsNoTracking().SingleAsync(u => u.PhoneNumber == phone);
        Assert.False(string.IsNullOrEmpty(persisted.SecurityStamp));
        Assert.NotEqual(Guid.Empty, persisted.Id);
    }
}
