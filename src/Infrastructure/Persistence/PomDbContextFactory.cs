using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace POM.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can construct <see cref="PomDbContext"/>
/// without booting the API host. The connection string here is a placeholder — migration
/// scaffolding only builds the model and never connects to a database. At runtime the real
/// connection string comes from configuration via <c>AddInfrastructure</c>.
/// </summary>
public sealed class PomDbContextFactory : IDesignTimeDbContextFactory<PomDbContext>
{
    public PomDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PomDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=obligation_test;Username=postgres;Password=postgres", npgsql =>
                npgsql.MigrationsAssembly(typeof(PomDbContext).Assembly.FullName));
        PomDbContext.ConfigureOptions(options);
        return new PomDbContext(options.Options);
    }
}


