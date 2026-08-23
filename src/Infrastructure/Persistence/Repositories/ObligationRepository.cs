using Microsoft.EntityFrameworkCore;
using POM.Obligations;
using POM.Obligations.Ports;

namespace POM.Persistence.Repositories;

internal sealed class ObligationRepository(PomDbContext dbContext) : IObligationRepository
{
    public Task<Obligation?> GetByIdAsync(Guid userId, Guid obligationId, CancellationToken cancellationToken = default) =>
        dbContext.Obligations.SingleOrDefaultAsync(x => x.Id == obligationId && x.UserId == userId && x.DeletedAt == null, cancellationToken);

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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
