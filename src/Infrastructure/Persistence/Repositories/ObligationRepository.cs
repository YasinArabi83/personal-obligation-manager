using Microsoft.EntityFrameworkCore;
using POM.Obligations;
using POM.Obligations.Ports;

namespace POM.Persistence.Repositories;

internal sealed class ObligationRepository(PomDbContext dbContext) : IObligationRepository
{
    public Task<Obligation?> GetByIdAsync(Guid userId, Guid obligationId, CancellationToken cancellationToken = default) =>
        dbContext.Obligations
            .SingleOrDefaultAsync(x => x.Id == obligationId && x.UserId == userId && x.DeletedAt == null, cancellationToken);

    public Task<Obligation?> GetByIdIncludingDeletedAsync(Guid userId, Guid obligationId, CancellationToken cancellationToken = default) =>
        dbContext.Obligations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == obligationId && x.UserId == userId, cancellationToken);

    public async Task<PagedResult<Obligation>> ListAsync(Guid userId, ObligationListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Obligations.Where(x => x.UserId == userId);

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);

        if (filter.Type.HasValue)
            query = query.Where(x => x.Type == filter.Type.Value);

        if (filter.CategoryId.HasValue)
            query = query.Where(x => x.CategoryId == filter.CategoryId.Value);

        if (filter.From.HasValue)
            query = query.Where(x => x.DueDate >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(x => x.DueDate <= filter.To.Value);

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var searchTerm = $"%{filter.Query}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, searchTerm) ||
                EF.Functions.ILike(x.Notes ?? "", searchTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.DueDate == null ? 1 : 0)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Obligation>(items, totalCount, page, pageSize);
    }

    public Task AddAsync(Obligation obligation, CancellationToken cancellationToken = default) =>
        dbContext.Obligations.AddAsync(obligation, cancellationToken).AsTask();

    public async Task<bool> UpdateAsync(Guid userId, Obligation obligation, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Obligations
            .SingleOrDefaultAsync(x => x.Id == obligation.Id && x.UserId == userId && x.DeletedAt == null, cancellationToken);
        if (existing is null) return false;
        dbContext.Entry(existing).CurrentValues.SetValues(obligation);
        return true;
    }

    public async Task<bool> UpdateIncludingDeletedAsync(Guid userId, Obligation obligation, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Obligations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == obligation.Id && x.UserId == userId, cancellationToken);
        if (existing is null) return false;
        dbContext.Entry(existing).CurrentValues.SetValues(obligation);
        return true;
    }

    public async Task<bool> SoftDeleteAsync(Guid userId, Guid obligationId, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Obligations
            .SingleOrDefaultAsync(x => x.Id == obligationId && x.UserId == userId && x.DeletedAt == null, cancellationToken);
        if (existing is null) return false;
        existing.SoftDelete();
        return true;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
