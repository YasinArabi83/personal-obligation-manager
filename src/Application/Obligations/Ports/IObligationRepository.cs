using POM.Obligations;

namespace POM.Obligations.Ports;

public interface IObligationRepository
{
    Task<Obligation?> GetByIdAsync(Guid userId, Guid obligationId, CancellationToken cancellationToken = default);
    Task AddAsync(Obligation obligation, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid userId, Obligation obligation, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
