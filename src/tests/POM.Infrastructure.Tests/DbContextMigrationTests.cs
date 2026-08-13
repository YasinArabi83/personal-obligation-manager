using Microsoft.EntityFrameworkCore;
using POM.Persistence;

namespace POM.Infrastructure.Tests;

/// <summary>
/// End-to-end check that the EF Core migration pipeline is wired correctly: the provider,
/// <see cref="PomDbContext"/>, the snake_case config, and the InitialCreate baseline migration
/// all apply cleanly against a real disposable Postgres. This de-risks 0003 (User entity):
/// a 0003 failure will be the entity config, not the pipeline.
/// </summary>
public sealed class DbContextMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DbContextMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_applies_and_context_can_connect()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PomDbContext>()
            .UseNpgsql(_fixture.ConnectionString);
        PomDbContext.ConfigureOptions(optionsBuilder);
        var options = optionsBuilder.Options;

        await using var context = new PomDbContext(options);

        // Applies the InitialCreate baseline (empty body) + provider-managed history table.
        await context.Database.MigrateAsync();

        var canConnect = await context.Database.CanConnectAsync();
        Assert.True(canConnect);
    }
}
