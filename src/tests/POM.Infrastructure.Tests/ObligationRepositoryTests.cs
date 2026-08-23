using Microsoft.EntityFrameworkCore;
using POM.Obligations;
using POM.Obligations.Ports;
using POM.Persistence;
using POM.Users;
using Xunit;

namespace POM.Infrastructure.Tests;

public sealed class ObligationRepositoryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture fixture;

    public ObligationRepositoryTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Repository_round_trips_and_isolates_by_user()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var userA = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        var userB = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        context.Users.AddRange(userA, userB);
        await context.SaveChangesAsync();

        var obligation = Obligation.Create(userA.Id, ObligationType.Task, "Private task",
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var repository = new POM.Persistence.Repositories.ObligationRepository(context);
        await repository.AddAsync(obligation);
        await repository.SaveChangesAsync();

        Assert.NotNull(await repository.GetByIdAsync(userA.Id, obligation.Id));
        Assert.Null(await repository.GetByIdAsync(userB.Id, obligation.Id));
        context.Entry(obligation).State = EntityState.Detached;
        obligation.UpdateDetails("Changed", obligation.DueDate);
        Assert.False(await repository.UpdateAsync(userB.Id, obligation));
    }

    private PomDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PomDbContext>()
            .UseNpgsql(fixture.ConnectionString);
        PomDbContext.ConfigureOptions(optionsBuilder);
        return new PomDbContext(optionsBuilder.Options);
    }
}
