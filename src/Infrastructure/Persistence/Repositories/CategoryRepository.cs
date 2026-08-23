using Microsoft.EntityFrameworkCore;
using POM.Taxonomy;
using POM.Taxonomy.Ports;

namespace POM.Persistence.Repositories;

internal sealed class CategoryRepository(PomDbContext dbContext) : ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> ListVisibleAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Categories.AsNoTracking()
            .Where(x => x.UserId == null || x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Category?> GetOwnedOrDefaultAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default) =>
        dbContext.Categories.SingleOrDefaultAsync(
            x => x.Id == categoryId && (x.UserId == userId || x.UserId == null), cancellationToken);

    public Task<bool> ExistsForUserAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default) =>
        dbContext.Categories.AnyAsync(x => x.Id == categoryId && (x.UserId == userId || x.UserId == null), cancellationToken);

    public Task<bool> IsReferencedByObligationAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default) =>
        dbContext.Obligations.AnyAsync(x => x.UserId == userId && x.CategoryId == categoryId, cancellationToken);

    public Task AddAsync(Category category, CancellationToken cancellationToken = default) =>
        dbContext.Categories.AddAsync(category, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public void Remove(Category category) => dbContext.Categories.Remove(category);
}
