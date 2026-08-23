using POM.Taxonomy;

namespace POM.Taxonomy.Ports;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> ListVisibleAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Category?> GetOwnedOrDefaultAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default);
    Task<bool> IsReferencedByObligationAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    void Remove(Category category);
}
