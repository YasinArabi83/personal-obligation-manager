using System.Text.Json.Serialization;
using POM.Obligations;
using POM.Obligations.Ports;

namespace POM.Obligations;

public sealed record ObligationDto(
    Guid Id,
    ObligationType Type,
    string Title,
    string? Notes,
    DateTime? StartDate,
    DateTime? DueDate,
    DateTime? EndDate,
    ObligationStatus Status,
    ObligationPriority Priority,
    Guid? CategoryId,
    string ExtraFields,
    bool IsRecurring,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? DeletedAt);

public sealed record ObligationCreateRequest(
    [property: JsonPropertyName("type")] ObligationType Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("dueDate")] DateTime? DueDate = null,
    [property: JsonPropertyName("startDate")] DateTime? StartDate = null,
    [property: JsonPropertyName("endDate")] DateTime? EndDate = null,
    [property: JsonPropertyName("notes")] string? Notes = null,
    [property: JsonPropertyName("priority")] ObligationPriority Priority = ObligationPriority.Medium,
    [property: JsonPropertyName("categoryId")] Guid? CategoryId = null,
    [property: JsonPropertyName("extraFields")] string? ExtraFields = null,
    [property: JsonPropertyName("isRecurring")] bool IsRecurring = false);

public sealed record ObligationUpdateRequest(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("dueDate")] DateTime? DueDate = null,
    [property: JsonPropertyName("startDate")] DateTime? StartDate = null,
    [property: JsonPropertyName("endDate")] DateTime? EndDate = null,
    [property: JsonPropertyName("notes")] string? Notes = null,
    [property: JsonPropertyName("priority")] ObligationPriority? Priority = null,
    [property: JsonPropertyName("categoryId")] Guid? CategoryId = null,
    [property: JsonPropertyName("extraFields")] string? ExtraFields = null);

public sealed record ObligationPostponeRequest(
    [property: JsonPropertyName("newDueDate")] DateTime NewDueDate);

public sealed class ObligationListRequest
{
    public ObligationStatus? Status { get; set; }

    public ObligationType? Type { get; set; }

    public Guid? CategoryId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string? Query { get; set; }

    public int? Page { get; set; }

    public int? PageSize { get; set; }
}

public sealed record ObligationListResponse(
    IReadOnlyList<ObligationDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public static class ObligationDtoExtensions
{
    public static ObligationDto ToDto(this Obligation obligation) =>
        new(
            obligation.Id,
            obligation.Type,
            obligation.Title,
            obligation.Notes,
            obligation.StartDate,
            obligation.DueDate,
            obligation.EndDate,
            obligation.Status,
            obligation.Priority,
            obligation.CategoryId,
            obligation.ExtraFields,
            obligation.IsRecurring,
            obligation.CreatedAt,
            obligation.UpdatedAt,
            obligation.DeletedAt);
}