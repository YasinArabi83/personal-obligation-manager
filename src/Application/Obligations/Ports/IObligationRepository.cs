using POM.Obligations;

namespace POM.Obligations.Ports;

public sealed record ObligationListFilter(
    ObligationStatus? Status = null,
    ObligationType? Type = null,
    Guid? CategoryId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Query = null);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public interface IObligationRepository
{
    Task<Obligation?> GetByIdAsync(Guid userId, Guid obligationId, CancellationToken cancellationToken = default);
    Task<Obligation?> GetByIdIncludingDeletedAsync(Guid userId, Guid obligationId, CancellationToken cancellationToken = default);
    Task<PagedResult<Obligation>> ListAsync(Guid userId, ObligationListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(Obligation obligation, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid userId, Obligation obligation, CancellationToken cancellationToken = default);
    Task<bool> UpdateIncludingDeletedAsync(Guid userId, Obligation obligation, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid userId, Guid obligationId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
