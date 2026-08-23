using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POM.Persistence;
using POM.Obligations.Ports;
using POM.Persistence.Repositories;
using POM.Persistence.Seeding;
using POM.Taxonomy;
using POM.Taxonomy.Ports;
using POM.Obligations;
using POM.Obligations.ExtraFields;

namespace POM.Persistence.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Infrastructure-layer services. Called once from the API composition root
    /// (Program.cs). This is the only sanctioned Infrastructure entry point the API layer
    /// references (see docs/ARCHITECTURE.md §2 — composition-root exception).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<PomDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(PomDbContext).Assembly.FullName));
            PomDbContext.ConfigureOptions(options);
        });

        services.AddScoped<IObligationRepository, ObligationRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<CategoryAppService>();
        services.AddScoped<DefaultCategorySeeder>();
        services.AddScoped<IExtraFieldsValidator, ExtraFieldsValidator>();
        services.AddScoped<ObligationAppService>();

        return services;
    }
}
