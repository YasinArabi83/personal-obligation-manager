using POM.Obligations;
using POM.Obligations.ExtraFields;
using Xunit;

namespace POM.Domain.Tests.Obligations;

public sealed class ObligationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime DueDate = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly IExtraFieldsValidator _validator = new ExtraFieldsValidator();

    [Fact]
    public void Create_applies_defaults_and_emits_event()
    {
        var obligation = Obligation.Create(UserId, ObligationType.Task, "  Rent  ", DueDate, extraFieldsValidator: _validator);

        Assert.Equal("Rent", obligation.Title);
        Assert.Equal(ObligationStatus.Pending, obligation.Status);
        Assert.Equal(ObligationPriority.Medium, obligation.Priority);
        Assert.Equal("{}", obligation.ExtraFields);
        Assert.Single(obligation.DomainEvents);
        Assert.IsType<ObligationCreated>(obligation.DomainEvents.Single());
    }

    [Fact]
    public void Create_with_null_due_date_is_allowed()
    {
        var obligation = Obligation.Create(UserId, ObligationType.Task, "No due date", dueDate: null);

        Assert.Null(obligation.DueDate);
        Assert.Equal(ObligationStatus.Pending, obligation.Status);
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

    [Fact]
    public void Postpone_rejects_when_no_due_date()
    {
        var obligation = Obligation.Create(UserId, ObligationType.Task, "x", dueDate: null);
        Assert.Throws<InvalidOperationException>(() => obligation.Postpone(DueDate.AddDays(1)));
    }

    [Fact]
    public void UpdateDetails_rejects_terminal_states()
    {
        var obligation = Obligation.Create(UserId, ObligationType.Task, "x", DueDate);
        obligation.Complete();
        Assert.Throws<InvalidOperationException>(() => obligation.UpdateDetails("y", DueDate, extraFieldsValidator: _validator));

        obligation = Obligation.Create(UserId, ObligationType.Task, "x", DueDate);
        obligation.Skip();
        Assert.Throws<InvalidOperationException>(() => obligation.UpdateDetails("y", DueDate, extraFieldsValidator: _validator));

        obligation = Obligation.Create(UserId, ObligationType.Task, "x", DueDate);
        obligation.Archive();
        Assert.Throws<InvalidOperationException>(() => obligation.UpdateDetails("y", DueDate, extraFieldsValidator: _validator));
    }

    [Fact]
    public void Restore_clears_deleted_at_and_emits_event()
    {
        var obligation = Obligation.Create(UserId, ObligationType.Task, "x", DueDate);
        obligation.SoftDelete();

        Assert.NotNull(obligation.DeletedAt);
        Assert.Single(obligation.DomainEvents); // Create event remains
        Assert.IsType<ObligationCreated>(obligation.DomainEvents.Single());

        obligation.Restore();

        Assert.Null(obligation.DeletedAt);
        Assert.Equal(2, obligation.DomainEvents.Count); // Create + Restore
        Assert.IsType<ObligationRestored>(obligation.DomainEvents.Last());
    }

    [Fact]
    public void Restore_is_idempotent()
    {
        var obligation = Obligation.Create(UserId, ObligationType.Task, "x", DueDate);
        obligation.SoftDelete();
        obligation.Restore();
        var eventsAfterFirstRestore = obligation.DomainEvents.Count;

        obligation.Restore(); // Second call should not add another event

        Assert.Equal(eventsAfterFirstRestore, obligation.DomainEvents.Count);
        Assert.Null(obligation.DeletedAt);
    }
}
