using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using POM.Obligations;

namespace POM.Endpoints;

public static class ObligationEndpoints
{
    public static IEndpointRouteBuilder MapObligationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/obligations")
            .RequireAuthorization()
            .WithTags("Obligations");

        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/complete", CompleteAsync);
        group.MapPost("/{id:guid}/postpone", PostponeAsync);
        group.MapPost("/{id:guid}/skip", SkipAsync);
        group.MapPost("/{id:guid}/archive", ArchiveAsync);
        group.MapPost("/{id:guid}/restore", RestoreAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext http,
        ObligationStatus? status,
        ObligationType? type,
        Guid? category,
        DateTime? from,
        DateTime? to,
        string? q,
        int? page,
        int? pageSize,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();

        var request = new ObligationListRequest
        {
            Status = status,
            Type = type,
            CategoryId = category,
            From = from,
            To = to,
            Query = q,
            Page = page,
            PageSize = pageSize
        };

        var result = await service.ListAsync(userId, request, ct);
        return result.Succeeded
            ? Results.Ok(result.Response)
            : Errors.From(result);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] ObligationCreateRequest? request,
        HttpContext http,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        if (request is null)
            return Errors.Validation("Request body is required.");

        var result = await service.CreateAsync(userId, request, ct);
        return result.Succeeded
            ? Results.Created($"/api/v1/obligations/{result.Obligation!.Id}", result.Obligation)
            : Errors.From(result);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext http,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        var result = await service.GetAsync(userId, id, ct);
        return result.Succeeded
            ? Results.Ok(result.Obligation)
            : Errors.From(result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] ObligationUpdateRequest? request,
        HttpContext http,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        if (request is null)
            return Errors.Validation("Request body is required.");

        var result = await service.UpdateAsync(userId, id, request, ct);
        return result.Succeeded
            ? Results.Ok(result.Obligation)
            : Errors.From(result);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext http,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        var result = await service.DeleteAsync(userId, id, ct);
        return result.Succeeded
            ? Results.NoContent()
            : Errors.From(result);
    }

    private static async Task<IResult> CompleteAsync(
        Guid id,
        HttpContext http,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        var result = await service.CompleteAsync(userId, id, ct);
        return result.Succeeded
            ? Results.NoContent()
            : Errors.From(result);
    }

    private static async Task<IResult> PostponeAsync(
        Guid id,
        [FromBody] ObligationPostponeRequest? request,
        HttpContext http,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        if (request is null)
            return Errors.Validation("Request body is required.");

        var result = await service.PostponeAsync(userId, id, request, ct);
        return result.Succeeded
            ? Results.NoContent()
            : Errors.From(result);
    }

    private static async Task<IResult> SkipAsync(
        Guid id,
        HttpContext http,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        var result = await service.SkipAsync(userId, id, ct);
        return result.Succeeded
            ? Results.NoContent()
            : Errors.From(result);
    }

    private static async Task<IResult> ArchiveAsync(
        Guid id,
        HttpContext http,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        var result = await service.ArchiveAsync(userId, id, ct);
        return result.Succeeded
            ? Results.NoContent()
            : Errors.From(result);
    }

    private static async Task<IResult> RestoreAsync(
        Guid id,
        HttpContext http,
        ObligationAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        var result = await service.RestoreAsync(userId, id, ct);
        return result.Succeeded
            ? Results.NoContent()
            : Errors.From(result);
    }

    private static bool TryGetUserId(HttpContext http, out Guid userId) =>
        Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId != Guid.Empty;

    private static class Errors
    {
        public static IResult Unauthorized() =>
            Results.Json(new { error = new { code = "unauthorized", message = "A valid JWT is required." } }, statusCode: 401);

        public static IResult Validation(string message) =>
            Results.Json(new { error = new { code = "validation_error", message } }, statusCode: 400);

        public static IResult From(ObligationCommandResult result) =>
            result.ErrorCode switch
            {
                "validation_error" => Validation(result.ErrorMessage!),
                "conflict" => Results.Json(new { error = new { code = "conflict", message = result.ErrorMessage } }, statusCode: 409),
                _ => Results.Json(new { error = new { code = "not_found", message = result.ErrorMessage ?? "Obligation was not found." } }, statusCode: 404),
            };

        public static IResult From(ObligationListResult result) =>
            result.ErrorCode switch
            {
                "validation_error" => Validation(result.ErrorMessage!),
                _ => Results.Json(new { error = new { code = "not_found", message = result.ErrorMessage ?? "Obligation was not found." } }, statusCode: 404),
            };

        public static IResult From(ObligationActionResult result) =>
            result.ErrorCode switch
            {
                "validation_error" => Validation(result.ErrorMessage!),
                "conflict" => Results.Json(new { error = new { code = "conflict", message = result.ErrorMessage } }, statusCode: 409),
                _ => Results.Json(new { error = new { code = "not_found", message = result.ErrorMessage ?? "Obligation was not found." } }, statusCode: 404),
            };
    }
}