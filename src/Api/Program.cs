using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using POM.Persistence.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure DI registration (DbContext, future ISmsSender/IFileStorage adapters).
// Controllers/Application services never call Infrastructure types directly — this is the
// sanctioned composition-root exception (docs/ARCHITECTURE.md §2).
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();
