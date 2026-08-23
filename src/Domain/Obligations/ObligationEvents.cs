namespace POM.Obligations;

public interface IObligationDomainEvent
{
    Guid ObligationId { get; }
    DateTime OccurredAt { get; }
}

public sealed record ObligationCreated(Guid ObligationId, DateTime OccurredAt) : IObligationDomainEvent;
public sealed record ObligationCompleted(Guid ObligationId, DateTime OccurredAt) : IObligationDomainEvent;
public sealed record ObligationSkipped(Guid ObligationId, DateTime OccurredAt) : IObligationDomainEvent;
public sealed record ObligationArchived(Guid ObligationId, DateTime OccurredAt) : IObligationDomainEvent;
