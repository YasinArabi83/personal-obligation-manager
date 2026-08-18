using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace POM;

/// <summary>
/// Swagger / OpenAPI document composition. Generates the <c>v1</c> document from the minimal-API
/// route metadata and registers a JWT Bearer security scheme so the Swagger UI "Authorize" button
/// can hold the access token minted by <c>otp/verify</c> (plan 0004). The UI is served only in
/// Development — never in production (docs/SECURITY.md). This lives in the API layer because it is
/// an ASP.NET Core hosting concern backed by the Web SDK shared framework.
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>JWT Bearer security scheme id referenced by the OpenAPI document.</summary>
    public const string BearerScheme = "Bearer";

    public static IServiceCollection AddPomSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Personal Obligation Manager API",
                Version = "v1",
                Description = "Phone + OTP auth. Obtain an access token via /api/v1/auth/otp/verify, then Authorize with \"Bearer {token}\".",
            });

            var bearer = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "JWT Bearer access token. Example: \"Bearer {token}\".",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
            };

            options.AddSecurityDefinition(BearerScheme, bearer);

            // Apply the scheme globally so every non-anonymous endpoint shows the lock icon.
            // Swashbuckle 10 + Microsoft.OpenApi 2.0: the requirement is provided as a factory
            // keyed by a typed security-scheme reference.
            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme)] = new List<string>(),
            });
        });

        return services;
    }
}
