using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POM.Persistence;
using POM.Persistence.Seeding;
using POM.Taxonomy;
using POM.Taxonomy.Ports;
using POM.Obligations;
using POM.Users;

namespace POM.Infrastructure.Tests;

public sealed class CategoryRepositoryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CategoryRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_seeds_defaults_and_visibility_is_user_scoped()
    {
        await using var provider = TestHost.Build(_fixture.ConnectionString);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PomDbContext>();
        await db.Database.MigrateAsync();

        var userA = User.Create("+989301111111");
        var userB = User.Create("+989302222222");
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        var repo = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
        var own = Category.CreateUser(userA.Id, "A only");
        await repo.AddAsync(own);
        await repo.SaveChangesAsync();

        var visibleA = await repo.ListVisibleAsync(userA.Id);
        var visibleB = await repo.ListVisibleAsync(userB.Id);

        Assert.Equal(DefaultCategorySeeder.Defaults.Count + 1, visibleA.Count);
        Assert.Equal(DefaultCategorySeeder.Defaults.Count, visibleB.Count);
        Assert.Contains(visibleA, x => x.Id == own.Id);
        Assert.DoesNotContain(visibleB, x => x.Id == own.Id);

        var seeder = scope.ServiceProvider.GetRequiredService<DefaultCategorySeeder>();
        await seeder.SeedAsync();
        Assert.Equal(DefaultCategorySeeder.Defaults.Count, await db.Categories.CountAsync(x => x.IsDefault));
    }

    [Fact]
    public async Task Referenced_category_delete_returns_conflict_and_keeps_obligation()
    {
        await using var provider = TestHost.Build(_fixture.ConnectionString);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PomDbContext>();
        await db.Database.MigrateAsync();

        var user = User.Create("+989303333333");
        db.Users.Add(user);
        var category = Category.CreateUser(user.Id, "Referenced");
        db.Categories.Add(category);
        db.Obligations.Add(Obligation.Create(user.Id, ObligationType.Task, "Keep me", DateTime.UtcNow.AddDays(1), categoryId: category.Id));
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<CategoryAppService>();
        var result = await service.DeleteAsync(user.Id, category.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("conflict", result.ErrorCode);
        Assert.True(await db.Categories.AnyAsync(x => x.Id == category.Id));
        Assert.True(await db.Obligations.AnyAsync(x => x.CategoryId == category.Id));
    }
}
