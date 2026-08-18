using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using POM.Persistence;
using System.Net.Http;
using Testcontainers.PostgreSql;

namespace POM.Api.Tests;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> over the real <c>Program</c> with the DbContext
/// swapped onto a disposable <c>postgres:18-alpine</c> container. The JWT signing key, SMS provider,
/// and a placeholder connection string come from <c>appsettings.Testing.json</c> (loaded because the
/// host runs under the <c>Testing</c> environment); only the DbContext connection is overridden here so
/// <c>AddInfrastructure</c>'s host-build connection check still passes.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("pom_api_tests")
        .WithUsername("pom")
        .WithPassword("dev")
        .Build();

    public string ConnectionString => _db.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Replace the DbContext with the test container connection (overrides AddInfrastructure's).
            services.RemoveAll<DbContextOptions<PomDbContext>>();
            services.AddDbContext<PomDbContext>(options =>
            {
                options.UseNpgsql(ConnectionString);
                PomDbContext.ConfigureOptions(options);
            });
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _db.StartAsync();

        // Apply migrations against the fresh container before any request runs.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PomDbContext>();
        await db.Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => _db.DisposeAsync().AsTask();
}
