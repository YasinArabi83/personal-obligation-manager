using System.Text.Json;
using POM.Obligations.ExtraFields;

namespace POM.Obligations;

public sealed class Obligation
{
    private readonly List<IObligationDomainEvent> domainEvents = new();

    private Obligation() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public ObligationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Notes { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public ObligationStatus Status { get; private set; }
    public ObligationPriority Priority { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string ExtraFields { get; private set; } = "{}";
    public bool IsRecurring { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public IReadOnlyCollection<IObligationDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public static Obligation Create(
        Guid userId,
        ObligationType type,
        string title,
        DateTime? dueDate = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? notes = null,
        ObligationPriority priority = ObligationPriority.Medium,
        Guid? categoryId = null,
        string? extraFields = null,
        bool isRecurring = false,
        DateTime? now = null,
        IExtraFieldsValidator? extraFieldsValidator = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User id is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));

        DateTime? utcDue = dueDate.HasValue ? ToUtc(dueDate.Value, nameof(dueDate)) : null;
        DateTime? utcStart = startDate.HasValue ? ToUtc(startDate.Value, nameof(startDate)) : null;
        DateTime? utcEnd = endDate.HasValue ? ToUtc(endDate.Value, nameof(endDate)) : null;
        ValidateDateRange(utcStart, utcDue, utcEnd);
        var timestamp = now.HasValue ? ToUtc(now.Value, nameof(now)) : DateTime.UtcNow;
        var normalizedExtra = NormalizeExtraFields(extraFields);

        if (extraFieldsValidator is not null)
        {
            var validation = extraFieldsValidator.Validate(type, normalizedExtra);
            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage ?? "ExtraFields validation failed.", nameof(extraFields));
        }

        var result = new Obligation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            StartDate = utcStart,
            DueDate = utcDue,
            EndDate = utcEnd,
            Status = ObligationStatus.Pending,
            Priority = priority,
            CategoryId = categoryId,
            ExtraFields = normalizedExtra,
            IsRecurring = isRecurring,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
        result.domainEvents.Add(new ObligationCreated(result.Id, timestamp));
        return result;
    }

    public void UpdateDetails(string title, DateTime? dueDate = null, DateTime? startDate = null, DateTime? endDate = null,
        string? notes = null, ObligationPriority? priority = null, Guid? categoryId = null, string? extraFields = null,
        IExtraFieldsValidator? extraFieldsValidator = null)
    {
        if (Status is ObligationStatus.Completed or ObligationStatus.Skipped or ObligationStatus.Archived)
            throw new InvalidOperationException("A closed obligation cannot be edited.");
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        DateTime? utcDue = dueDate.HasValue ? ToUtc(dueDate.Value, nameof(dueDate)) : null;
        DateTime? utcStart = startDate.HasValue ? ToUtc(startDate.Value, nameof(startDate)) : null;
        DateTime? utcEnd = endDate.HasValue ? ToUtc(endDate.Value, nameof(endDate)) : null;
        ValidateDateRange(utcStart, utcDue, utcEnd);
        Title = title.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        StartDate = utcStart;
        DueDate = utcDue;
        EndDate = utcEnd;
        if (priority.HasValue) Priority = priority.Value;
        CategoryId = categoryId;
        var newExtraFields = extraFields is not null ? NormalizeExtraFields(extraFields) : ExtraFields;
        if (extraFieldsValidator is not null)
        {
            var validation = extraFieldsValidator.Validate(Type, newExtraFields);
            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage ?? "ExtraFields validation failed.", nameof(extraFields));
        }
        ExtraFields = newExtraFields;
        Touch();
    }

    public void Complete()
    {
        EnsureTransition(ObligationStatus.Completed);
        Status = ObligationStatus.Completed;
        Touch();
        domainEvents.Add(new ObligationCompleted(Id, UpdatedAt));
    }

    public void Skip()
    {
        EnsureTransition(ObligationStatus.Skipped);
        Status = ObligationStatus.Skipped;
        Touch();
        domainEvents.Add(new ObligationSkipped(Id, UpdatedAt));
    }

    public void MarkOverdue()
    {
        if (Status != ObligationStatus.Pending) throw new InvalidOperationException("Only pending obligations can become overdue.");
        Status = ObligationStatus.Overdue;
        Touch();
    }

    public void Archive()
    {
        if (Status == ObligationStatus.Archived) return;
        Status = ObligationStatus.Archived;
        Touch();
        domainEvents.Add(new ObligationArchived(Id, UpdatedAt));
    }

    public void Postpone(DateTime newDueDate)
    {
        if (Status is ObligationStatus.Completed or ObligationStatus.Skipped or ObligationStatus.Archived)
            throw new InvalidOperationException("A closed obligation cannot be postponed.");
        if (!DueDate.HasValue)
            throw new InvalidOperationException("Cannot postpone an obligation without a due date.");
        var utcDue = ToUtc(newDueDate, nameof(newDueDate));
        ValidateDateRange(StartDate, utcDue, EndDate);
        DueDate = utcDue;
        if (Status == ObligationStatus.Overdue) Status = ObligationStatus.Pending;
        Touch();
    }

    public void SoftDelete(DateTime? at = null)
    {
        DeletedAt ??= at.HasValue ? ToUtc(at.Value, nameof(at)) : DateTime.UtcNow;
        Touch();
    }

    public void Restore(DateTime? at = null)
    {
        if (!DeletedAt.HasValue) return;
        DeletedAt = null;
        Touch();
        domainEvents.Add(new ObligationRestored(Id, at.HasValue ? ToUtc(at.Value, nameof(at)) : DateTime.UtcNow));
    }

    public void ClearDomainEvents() => domainEvents.Clear();

    private void EnsureTransition(ObligationStatus target)
    {
        if (Status is ObligationStatus.Completed or ObligationStatus.Skipped or ObligationStatus.Archived)
            throw new InvalidOperationException($"Cannot transition {Status} obligation to {target}.");
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;

    private static string NormalizeExtraFields(string? extraFields)
    {
        if (string.IsNullOrWhiteSpace(extraFields)) return "{}";
        try
        {
            using var document = JsonDocument.Parse(extraFields);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("ExtraFields must be a JSON object.", nameof(extraFields));
            return document.RootElement.GetRawText();
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("ExtraFields must be valid JSON.", nameof(extraFields), ex);
        }
    }

    private static void ValidateDateRange(DateTime? start, DateTime? due, DateTime? end)
    {
        if (start.HasValue && due.HasValue && start.Value > due.Value) throw new ArgumentException("Start date must be on or before due date.");
        if (end.HasValue && due.HasValue && due.Value > end.Value) throw new ArgumentException("Due date must be on or before end date.");
    }

    private static DateTime ToUtc(DateTime value, string parameterName) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
