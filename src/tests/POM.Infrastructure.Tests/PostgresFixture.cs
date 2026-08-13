using Testcontainers.PostgreSql;

namespace POM.Infrastructure.Tests;

/// <summary>
/// Shared fixture that starts a disposable <c>postgres:18-alpine</c> container once per test
/// collection (ADR-0010 pins Postgres 18). Integration tests run against a real database, not
/// a mock — see docs/TESTING.md §3.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("pom_tests")
        .WithUsername("pom")
        .WithPassword("dev")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync() => await _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
