using POM.Obligations;
using Xunit;

namespace POM.Domain.Tests.Obligations;

public sealed class ObligationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime DueDate = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_applies_defaults_and_emits_event()
    {
        var obligation = Obligation.Create(UserId, ObligationType.Payment, "  Rent  ", DueDate);

        Assert.Equal("Rent", obligation.Title);
        Assert.Equal(ObligationStatus.Pending, obligation.Status);
        Assert.Equal(ObligationPriority.Medium, obligation.Priority);
        Assert.Equal("{}", obligation.ExtraFields);
        Assert.Single(obligation.DomainEvents);
        Assert.IsType<ObligationCreated>(obligation.DomainEvents.Single());
    }

    [Fact]
    public void Create_rejects_invalid_date_range_and_non_object_extra_fields()
    {
        Assert.Throws<ArgumentException>(() => Obligation.Create(UserId, ObligationType.Task, "x", DueDate, DueDate.AddDays(1)));
        Assert.Throws<ArgumentException>(() => Obligation.Create(UserId, ObligationType.Task, "x", DueDate, extraFields: "[1]"));
    }

    [Fact]
    public void Complete_and_archive_follow_transitions()
    {
        var obligation = Obligation.Create(UserId, ObligationType.Task, "x", DueDate);
        obligation.Complete();
        Assert.Equal(ObligationStatus.Completed, obligation.Status);
        Assert.Throws<InvalidOperationException>(() => obligation.Skip());
        obligation.Archive();
        Assert.Equal(ObligationStatus.Archived, obligation.Status);
    }

    [Fact]
    public void Postpone_resets_overdue_to_pending()
    {
        var obligation = Obligation.Create(UserId, ObligationType.Task, "x", DueDate);
        obligation.MarkOverdue();
        obligation.Postpone(DueDate.AddDays(2));
        Assert.Equal(ObligationStatus.Pending, obligation.Status);
        Assert.Equal(DueDate.AddDays(2), obligation.DueDate);
    }
}
