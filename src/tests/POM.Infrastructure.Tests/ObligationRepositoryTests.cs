using Microsoft.EntityFrameworkCore;
using POM.Obligations;
using POM.Obligations.Ports;
using POM.Persistence;
using POM.Persistence.Repositories;
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
        var repository = new ObligationRepository(context);
        await repository.AddAsync(obligation);
        await repository.SaveChangesAsync();

        Assert.NotNull(await repository.GetByIdAsync(userA.Id, obligation.Id));
        Assert.Null(await repository.GetByIdAsync(userB.Id, obligation.Id));
        context.Entry(obligation).State = EntityState.Detached;
        obligation.UpdateDetails("Changed", obligation.DueDate);
        Assert.False(await repository.UpdateAsync(userB.Id, obligation));
    }

    [Fact]
    public async Task ListAsync_filters_by_status_type_category_date_and_query()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var user = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repo = new ObligationRepository(context);
        var baseDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var o1 = Obligation.Create(user.Id, ObligationType.Task, "Task A", baseDate);
        var o2 = Obligation.Create(user.Id, ObligationType.Payment, "Payment B", baseDate.AddDays(5));
        var o3 = Obligation.Create(user.Id, ObligationType.Task, "Task C", baseDate.AddDays(10));
        o3.Complete();
        await repo.AddAsync(o1);
        await repo.AddAsync(o2);
        await repo.AddAsync(o3);
        await repo.SaveChangesAsync();

        var all = await repo.ListAsync(user.Id, new ObligationListFilter(), 1, 10);
        Assert.Equal(3, all.TotalCount);

        var pending = await repo.ListAsync(user.Id, new ObligationListFilter(Status: ObligationStatus.Pending), 1, 10);
        Assert.Equal(2, pending.TotalCount);
        Assert.All(pending.Items, x => Assert.Equal(ObligationStatus.Pending, x.Status));

        var taskType = await repo.ListAsync(user.Id, new ObligationListFilter(Type: ObligationType.Task), 1, 10);
        Assert.Equal(2, taskType.TotalCount);
        Assert.All(taskType.Items, x => Assert.Equal(ObligationType.Task, x.Type));

        var completed = await repo.ListAsync(user.Id, new ObligationListFilter(Status: ObligationStatus.Completed), 1, 10);
        Assert.Equal(1, completed.TotalCount);

        var search = await repo.ListAsync(user.Id, new ObligationListFilter(Query: "Payment"), 1, 10);
        Assert.Equal(1, search.TotalCount);
        Assert.Equal("Payment B", search.Items[0].Title);

        var fromFilter = await repo.ListAsync(user.Id, new ObligationListFilter(From: baseDate.AddDays(3)), 1, 10);
        Assert.Equal(2, fromFilter.TotalCount);

        var toFilter = await repo.ListAsync(user.Id, new ObligationListFilter(To: baseDate.AddDays(3)), 1, 10);
        Assert.Equal(1, toFilter.TotalCount);
    }

    [Fact]
    public async Task ListAsync_orders_by_due_date_asc_nulls_last()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var user = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repo = new ObligationRepository(context);
        var baseDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var o1 = Obligation.Create(user.Id, ObligationType.Task, "No due date", dueDate: null);
        var o2 = Obligation.Create(user.Id, ObligationType.Task, "Due Sept 5", baseDate.AddDays(4));
        var o3 = Obligation.Create(user.Id, ObligationType.Task, "Due Sept 1", baseDate);
        await repo.AddAsync(o1);
        await repo.AddAsync(o2);
        await repo.AddAsync(o3);
        await repo.SaveChangesAsync();

        var result = await repo.ListAsync(user.Id, new ObligationListFilter(), 1, 10);

        Assert.Equal("Due Sept 1", result.Items[0].Title);
        Assert.Equal("Due Sept 5", result.Items[1].Title);
        Assert.Equal("No due date", result.Items[2].Title);
    }

    [Fact]
    public async Task ListAsync_pagination_works()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var user = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repo = new ObligationRepository(context);
        var baseDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
        {
            var o = Obligation.Create(user.Id, ObligationType.Task, $"Task {i}", baseDate.AddDays(i));
            await repo.AddAsync(o);
        }
        await repo.SaveChangesAsync();

        var page1 = await repo.ListAsync(user.Id, new ObligationListFilter(), 1, 2);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);

        var page2 = await repo.ListAsync(user.Id, new ObligationListFilter(), 2, 2);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal("Task 2", page2.Items[0].Title);
    }

    [Fact]
    public async Task SoftDelete_excludes_from_normal_queries()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var user = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repo = new ObligationRepository(context);
        var o = Obligation.Create(user.Id, ObligationType.Task, "To delete", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        await repo.AddAsync(o);
        await repo.SaveChangesAsync();

        Assert.NotNull(await repo.GetByIdAsync(user.Id, o.Id));

        o.SoftDelete();
        await repo.UpdateAsync(user.Id, o);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetByIdAsync(user.Id, o.Id));

        var list = await repo.ListAsync(user.Id, new ObligationListFilter(), 1, 10);
        Assert.Equal(0, list.TotalCount);
    }

    [Fact]
    public async Task GetByIdIncludingDeletedAsync_finds_soft_deleted()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var user = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repo = new ObligationRepository(context);
        var o = Obligation.Create(user.Id, ObligationType.Task, "To delete", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        await repo.AddAsync(o);
        await repo.SaveChangesAsync();

        o.SoftDelete();
        await repo.UpdateAsync(user.Id, o);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetByIdAsync(user.Id, o.Id));
        Assert.NotNull(await repo.GetByIdIncludingDeletedAsync(user.Id, o.Id));
    }

    [Fact]
    public async Task Restore_bypasses_global_filter_and_restores()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var user = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repo = new ObligationRepository(context);
        var o = Obligation.Create(user.Id, ObligationType.Task, "To restore", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        await repo.AddAsync(o);
        await repo.SaveChangesAsync();

        o.SoftDelete();
        await repo.UpdateAsync(user.Id, o);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetByIdAsync(user.Id, o.Id));

        var deleted = await repo.GetByIdIncludingDeletedAsync(user.Id, o.Id);
        Assert.NotNull(deleted);
        deleted!.Restore();
        await repo.UpdateIncludingDeletedAsync(user.Id, deleted);
        await repo.SaveChangesAsync();

        Assert.NotNull(await repo.GetByIdAsync(user.Id, o.Id));
        Assert.Null((await repo.GetByIdAsync(user.Id, o.Id))!.DeletedAt);
    }

    [Fact]
    public async Task User_isolation_on_restore()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var userA = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        var userB = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        context.Users.AddRange(userA, userB);
        await context.SaveChangesAsync();

        var repo = new ObligationRepository(context);
        var o = Obligation.Create(userA.Id, ObligationType.Task, "User A task", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        await repo.AddAsync(o);
        await repo.SaveChangesAsync();

        o.SoftDelete();
        await repo.UpdateAsync(userA.Id, o);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetByIdIncludingDeletedAsync(userB.Id, o.Id));
        Assert.NotNull(await repo.GetByIdIncludingDeletedAsync(userA.Id, o.Id));

        var deleted = await repo.GetByIdIncludingDeletedAsync(userB.Id, o.Id);
        Assert.Null(deleted);

        deleted = await repo.GetByIdIncludingDeletedAsync(userA.Id, o.Id);
        Assert.NotNull(deleted);
        deleted!.Restore();
        await repo.UpdateIncludingDeletedAsync(userA.Id, deleted);
        await repo.SaveChangesAsync();

        Assert.NotNull(await repo.GetByIdAsync(userA.Id, o.Id));
        Assert.Null(await repo.GetByIdAsync(userB.Id, o.Id));
    }

    [Fact]
    public async Task ILIKE_search_is_case_insensitive()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var user = User.Create($"+989{Random.Shared.NextInt64(100000000, 999999999)}");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repo = new ObligationRepository(context);
        var baseDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var o = Obligation.Create(user.Id, ObligationType.Task, "Rent Payment", baseDate);
        await repo.AddAsync(o);
        await repo.SaveChangesAsync();

        var result = await repo.ListAsync(user.Id, new ObligationListFilter(Query: "rent"), 1, 10);
        Assert.Equal(1, result.TotalCount);

        result = await repo.ListAsync(user.Id, new ObligationListFilter(Query: "RENT"), 1, 10);
        Assert.Equal(1, result.TotalCount);

        result = await repo.ListAsync(user.Id, new ObligationListFilter(Query: "payment"), 1, 10);
        Assert.Equal(1, result.TotalCount);
    }

    private PomDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PomDbContext>()
            .UseNpgsql(fixture.ConnectionString);
        PomDbContext.ConfigureOptions(optionsBuilder);
        return new PomDbContext(optionsBuilder.Options);
    }
}
