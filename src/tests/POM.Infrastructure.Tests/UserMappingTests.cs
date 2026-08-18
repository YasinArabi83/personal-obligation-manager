using Microsoft.EntityFrameworkCore;
using POM.Persistence;

namespace POM.Infrastructure.Tests;

/// <summary>
/// Verifies the <c>users</c> table is created exactly as documented (DATABASE.md §3 + ADR-0016)
/// when the migration is applied against a real Postgres, and — critically — that <b>no
/// <c>AspNet*</c> Identity tables are created</b> (the custom store keeps Identity off the schema).
/// </summary>
public sealed class UserMappingTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public UserMappingTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Users_table_has_exactly_the_documented_columns_and_unique_phone_index()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PomDbContext>().UseNpgsql(_fixture.ConnectionString);
        PomDbContext.ConfigureOptions(optionsBuilder);
        await using var context = new PomDbContext(optionsBuilder.Options);

        await context.Database.MigrateAsync();

        var columns = await context.Database.SqlQueryRaw<string>(
                "select column_name from information_schema.columns where table_name = 'users' order by column_name")
            .ToListAsync();

        Assert.Equal(
            new[] { "calendar_pref", "created_at", "deleted_at", "display_name", "id",
                    "phone_number", "phone_number_confirmed", "security_stamp" },
            columns);

        // Unique index on phone_number.
        var uniqueIndexCount = await context.Database.SqlQueryRaw<int>(
                @"select count(*)::int as ""Value"" from pg_indexes
                  where tablename = 'users' and indexdef ilike '%UNIQUE%' and indexdef ilike '%phone_number%'")
            .FirstAsync();

        Assert.Equal(1, uniqueIndexCount);
    }

    [Fact]
    public async Task No_AspNet_identity_tables_are_created()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PomDbContext>().UseNpgsql(_fixture.ConnectionString);
        PomDbContext.ConfigureOptions(optionsBuilder);
        await using var context = new PomDbContext(optionsBuilder.Options);

        await context.Database.MigrateAsync();

        var aspNetTables = await context.Database.SqlQueryRaw<string>(
                "select table_name from information_schema.tables where table_name like 'aspnet%'")
            .ToListAsync();

        Assert.Empty(aspNetTables);
    }
}
