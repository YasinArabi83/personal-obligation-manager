using POM.Obligations.Ports;
using POM.Taxonomy.Ports;
using POM.Obligations.ExtraFields;

namespace POM.Obligations;

public sealed class ObligationAppService(
    IObligationRepository obligationRepository,
    ICategoryRepository categoryRepository,
    IExtraFieldsValidator extraFieldsValidator)
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public async Task<ObligationCommandResult> CreateAsync(Guid userId, ObligationCreateRequest request, CancellationToken ct = default)
    {
        EnsureUser(userId);

        if (request.CategoryId.HasValue)
        {
            var category = await categoryRepository.GetOwnedOrDefaultAsync(userId, request.CategoryId.Value, ct);
            if (category is null)
                return ObligationCommandResult.Failure("validation_error", "Category not found or not accessible.");
        }

        try
        {
            var obligation = Obligation.Create(
                userId,
                request.Type,
                request.Title,
                request.DueDate,
                request.StartDate,
                request.EndDate,
                request.Notes,
                request.Priority,
                request.CategoryId,
                request.ExtraFields,
                request.IsRecurring,
                now: null,
                extraFieldsValidator);

            await obligationRepository.AddAsync(obligation, ct);
            await obligationRepository.SaveChangesAsync(ct);
            obligation.ClearDomainEvents();

            return new ObligationCommandResult(obligation.ToDto(), null, null);
        }
        catch (ArgumentException ex)
        {
            return ObligationCommandResult.Failure("validation_error", ex.Message);
        }
    }

    public async Task<ObligationCommandResult> GetAsync(Guid userId, Guid obligationId, CancellationToken ct = default)
    {
        EnsureUser(userId);

        var obligation = await obligationRepository.GetByIdAsync(userId, obligationId, ct);
        if (obligation is null)
            return ObligationCommandResult.Failure("not_found", "Obligation was not found.");

        return new ObligationCommandResult(obligation.ToDto(), null, null);
    }

    public async Task<ObligationListResult> ListAsync(Guid userId, ObligationListRequest request, CancellationToken ct = default)
    {
        EnsureUser(userId);

        var page = request.Page is > 0 ? request.Page.Value : 1;
        var pageSize = request.PageSize is > 0 ? Math.Min(request.PageSize.Value, MaxPageSize) : DefaultPageSize;

        var filter = new ObligationListFilter(
            request.Status,
            request.Type,
            request.CategoryId,
            AsUtc(request.From),
            AsUtc(request.To),
            request.Query);

        var result = await obligationRepository.ListAsync(userId, filter, page, pageSize, ct);

        var response = new ObligationListResponse(
            result.Items.Select(o => o.ToDto()).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);

        return new ObligationListResult(response, null, null);
    }

    public async Task<ObligationCommandResult> UpdateAsync(Guid userId, Guid obligationId, ObligationUpdateRequest request, CancellationToken ct = default)
    {
        EnsureUser(userId);

        var obligation = await obligationRepository.GetByIdAsync(userId, obligationId, ct);
        if (obligation is null)
            return ObligationCommandResult.Failure("not_found", "Obligation was not found.");

        if (request.CategoryId.HasValue)
        {
            var category = await categoryRepository.GetOwnedOrDefaultAsync(userId, request.CategoryId.Value, ct);
            if (category is null)
                return ObligationCommandResult.Failure("validation_error", "Category not found or not accessible.");
        }

        try
        {
            obligation.UpdateDetails(
                request.Title,
                request.DueDate,
                request.StartDate,
                request.EndDate,
                request.Notes,
                request.Priority,
                request.CategoryId,
                request.ExtraFields,
                extraFieldsValidator);

            var updated = await obligationRepository.UpdateAsync(userId, obligation, ct);
            if (!updated)
                return ObligationCommandResult.Failure("not_found", "Obligation was not found.");

            await obligationRepository.SaveChangesAsync(ct);
            obligation.ClearDomainEvents();

            return new ObligationCommandResult(obligation.ToDto(), null, null);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("closed obligation"))
        {
            return ObligationCommandResult.Failure("conflict", "A closed obligation cannot be edited.");
        }
        catch (ArgumentException ex)
        {
            return ObligationCommandResult.Failure("validation_error", ex.Message);
        }
    }

    public async Task<ObligationActionResult> DeleteAsync(Guid userId, Guid obligationId, CancellationToken ct = default)
    {
        EnsureUser(userId);

        var obligation = await obligationRepository.GetByIdAsync(userId, obligationId, ct);
        if (obligation is null)
            return ObligationActionResult.Failure("not_found", "Obligation was not found.");

        obligation.SoftDelete();
        var updated = await obligationRepository.UpdateAsync(userId, obligation, ct);
        if (!updated)
            return ObligationActionResult.Failure("not_found", "Obligation was not found.");

        await obligationRepository.SaveChangesAsync(ct);
        obligation.ClearDomainEvents();

        return ObligationActionResult.Success();
    }

    public async Task<ObligationActionResult> CompleteAsync(Guid userId, Guid obligationId, CancellationToken ct = default)
        => await ExecuteActionAsync(userId, obligationId, o => o.Complete(), ct);

    public async Task<ObligationActionResult> PostponeAsync(Guid userId, Guid obligationId, ObligationPostponeRequest request, CancellationToken ct = default)
        => await ExecuteActionAsync(userId, obligationId, o => o.Postpone(request.NewDueDate), ct);

    public async Task<ObligationActionResult> SkipAsync(Guid userId, Guid obligationId, CancellationToken ct = default)
        => await ExecuteActionAsync(userId, obligationId, o => o.Skip(), ct);

    public async Task<ObligationActionResult> ArchiveAsync(Guid userId, Guid obligationId, CancellationToken ct = default)
        => await ExecuteActionAsync(userId, obligationId, o => o.Archive(), ct);

    public async Task<ObligationActionResult> RestoreAsync(Guid userId, Guid obligationId, CancellationToken ct = default)
    {
        EnsureUser(userId);

        var obligation = await obligationRepository.GetByIdIncludingDeletedAsync(userId, obligationId, ct);
        if (obligation is null)
            return ObligationActionResult.Failure("not_found", "Obligation was not found.");

        if (!obligation.DeletedAt.HasValue)
            return ObligationActionResult.Failure("conflict", "Obligation is not deleted.");

        obligation.Restore();
        var updated = await obligationRepository.UpdateIncludingDeletedAsync(userId, obligation, ct);
        if (!updated)
            return ObligationActionResult.Failure("not_found", "Obligation was not found.");

        await obligationRepository.SaveChangesAsync(ct);
        obligation.ClearDomainEvents();

        return ObligationActionResult.Success();
    }

    private async Task<ObligationActionResult> ExecuteActionAsync(
        Guid userId,
        Guid obligationId,
        Action<Obligation> action,
        CancellationToken ct)
    {
        EnsureUser(userId);

        var obligation = await obligationRepository.GetByIdAsync(userId, obligationId, ct);
        if (obligation is null)
            return ObligationActionResult.Failure("not_found", "Obligation was not found.");

        try
        {
            action(obligation);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("closed obligation") || ex.Message.Contains("Cannot transition"))
        {
            return ObligationActionResult.Failure("conflict", "A closed obligation cannot be edited.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("due date"))
        {
            return ObligationActionResult.Failure("validation_error", ex.Message);
        }

        var updated = await obligationRepository.UpdateAsync(userId, obligation, ct);
        if (!updated)
            return ObligationActionResult.Failure("not_found", "Obligation was not found.");

        await obligationRepository.SaveChangesAsync(ct);
        obligation.ClearDomainEvents();

        return ObligationActionResult.Success();
    }

    private static void EnsureUser(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User id is required.", nameof(userId));
    }

    private static DateTime? AsUtc(DateTime? value) =>
        value is null
            ? null
            : value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
}