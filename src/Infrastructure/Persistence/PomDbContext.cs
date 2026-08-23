using Microsoft.EntityFrameworkCore;
using POM.Auth.Jwt;
using POM.Obligations;
using POM.Users;

namespace POM.Persistence;

/// <summary>
/// Application <see cref="DbContext"/>. This is the single EF Core entry point for the
/// whole modular monolith; module boundaries are code/namespace boundaries, not separate
/// DbContexts (see docs/ARCHITECTURE.md §4).
/// </summary>
/// <remarks>
/// Entity configurations live in <c>Persistence.Configurations.&lt;Aggregate&gt;</c> and are
/// applied automatically via <see cref="OnModelCreating"/>. Snake_case table/column naming
/// (docs/DATABASE.md §2) is applied via <see cref="ConfigureOptions"/> so every entity is named
/// correctly without per-entity manual <c>ToTable</c>/<c>HasColumnName</c>. <see cref="ConfigureOptions"/>
/// is the single place options are tuned; both the DI composition root and integration tests call
/// it so a test never drifts from production configuration.
/// </remarks>
public class PomDbContext : DbContext
{
    public PomDbContext(DbContextOptions<PomDbContext> options) : base(options)
    {
    }

    /// <summary>Users aggregate. The custom Identity <c>UserStore</c> reads/writes through this.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Refresh tokens (auth store, not a Domain aggregate — plan 0004 Q3). Raw tokens are never
    /// stored; only their SHA-256 hash (<see cref="Jwt.RefreshToken.TokenHash"/>).
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Obligation> Obligations => Set<Obligation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Pick up every IEntityTypeConfiguration<> in this assembly (e.g. UserConfiguration).
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PomDbContext).Assembly);
    }

    /// <summary>
    /// Centralizes non-provider options (naming convention) so the composition root and
    /// integration tests build identical options. Call after the provider is selected.
    /// </summary>
    public static void ConfigureOptions(DbContextOptionsBuilder options)
    {
        // Apply snake_case naming globally — tables, columns, keys, indexes.
        // EFCore.NamingConventions exposes this as an options-builder extension (10.x API).
        // See ADR-0014 in docs/DECISIONS.md.
        options.UseSnakeCaseNamingConvention();
    }
}
