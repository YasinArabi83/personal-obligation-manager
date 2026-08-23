using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;
using POM;
using POM.Endpoints;
using POM.Auth.RateLimiting;
using POM.Identity.DependencyInjection;
using POM.Persistence.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON options for enum string serialization/deserialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Infrastructure DI registration (DbContext, future ISmsSender/IFileStorage adapters).
// Controllers/Application services never call Infrastructure types directly — this is the
// sanctioned composition-root exception (docs/ARCHITECTURE.md §2).
builder.Services.AddInfrastructure(builder.Configuration);

// OTP-only ASP.NET Core Identity (custom store over the clean `users` table, built-in
// PhoneNumberTokenProvider, no password). HTTP auth endpoints/JWT land in 0004.
builder.Services.AddAuth(builder.Configuration);

// OTP auth port implementations (SMS sender, OTP sender/verifier, JWT token service, per-phone
// rate limiter) + the AuthAppService orchestration (plan 0004).
builder.Services.AddOtpAuthInfrastructure(builder.Configuration);

// API composition: JWT bearer validation + authorization + the coarse per-IP rate limiter.
builder.Services.AddPomApiAuthentication(builder.Configuration);

// Swagger / OpenAPI document + JWT Bearer security scheme. UI is served in Development only.
builder.Services.AddPomSwagger();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Auth routes: anonymous (no JWT yet), gated by per-IP + per-phone rate limiting.
app.MapAuthEndpoints();
app.MapCategoryEndpoints();
app.MapObligationEndpoints();

// Health check stays public; every other future endpoint calls RequireAuthorization().
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

app.Run();

// Make the top-level Program class referenceable by WebApplicationFactory in integration tests.
public partial class Program { }
