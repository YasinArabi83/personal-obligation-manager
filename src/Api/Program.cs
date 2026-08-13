using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure DI registration will be wired here in task 0002
// (e.g. builder.Services.AddInfrastructure(builder.Configuration)).
// Controllers/Application services never call Infrastructure types directly.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();
