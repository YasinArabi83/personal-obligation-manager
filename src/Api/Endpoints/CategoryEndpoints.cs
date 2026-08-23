using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using POM.Taxonomy;

namespace POM.Endpoints;

public sealed record CategoryRequest([property: Required] string Name, string? Icon);

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories")
            .RequireAuthorization()
            .WithTags("Categories");

        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(HttpContext http, CategoryAppService service, CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        var categories = await service.ListAsync(userId, ct);
        return Results.Ok(categories);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CategoryRequest? request,
        HttpContext http,
        CategoryAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            return Errors.Validation("name is required.");

        var result = await service.CreateAsync(userId, request.Name, request.Icon, ct);
        return result.Succeeded
            ? Results.Created($"/api/v1/categories/{result.Category!.Id}", result.Category)
            : Errors.From(result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] CategoryRequest? request,
        HttpContext http,
        CategoryAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            return Errors.Validation("name is required.");

        var result = await service.UpdateAsync(userId, id, request.Name, request.Icon, ct);
        return result.Succeeded ? Results.Ok(result.Category) : Errors.From(result);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext http,
        CategoryAppService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(http, out var userId)) return Errors.Unauthorized();
        var result = await service.DeleteAsync(userId, id, ct);
        return result.Succeeded ? Results.NoContent() : Errors.From(result);
    }

    private static bool TryGetUserId(HttpContext http, out Guid userId) =>
        Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId != Guid.Empty;

    private static class Errors
    {
        public static IResult Unauthorized() =>
            Results.Json(new { error = new { code = "unauthorized", message = "A valid JWT is required." } }, statusCode: 401);

        public static IResult Validation(string message) =>
            Results.Json(new { error = new { code = "validation_error", message } }, statusCode: 400);

        public static IResult From(CategoryCommandResult result) =>
            result.ErrorCode switch
            {
                "validation_error" => Validation(result.ErrorMessage!),
                "conflict" => Results.Json(new { error = new { code = "conflict", message = result.ErrorMessage } }, statusCode: 409),
                _ => Results.Json(new { error = new { code = "not_found", message = result.ErrorMessage ?? "Category was not found." } }, statusCode: 404),
            };
    }
}
