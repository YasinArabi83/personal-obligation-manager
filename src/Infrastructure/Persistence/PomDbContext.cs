using Microsoft.EntityFrameworkCore;

namespace POM.Persistence;

/// <summary>
/// Application <see cref="DbContext"/>. This is the single EF Core entry point for the
/// whole modular monolith; module boundaries are code/namespace boundaries, not separate
/// DbContexts (see docs/ARCHITECTURE.md §4).
/// </summary>
/// <remarks>
/// No <c>DbSet&lt;&gt;</c> properties yet — entity configurations land with their tasks
/// (<c>User</c> → 0003, <c>Obligation</c> → 0006, etc.). The first migration (InitialCreate)
/// is intentionally an empty baseline that locks in the pipeline before any business table.
/// Snake_case table/column naming (docs/DATABASE.md §2) is applied via
/// <see cref="ConfigureOptions"/> so every future entity is named correctly without per-entity
/// manual <c>ToTable</c>/<c>HasColumnName</c>. <see cref="ConfigureOptions"/> is the single
/// place options are tuned; both the DI composition root and integration tests call it so a
/// test never drifts from production configuration.
/// </remarks>
public class PomDbContext : DbContext
{
    public PomDbContext(DbContextOptions<PomDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Centralizes non-provider options (naming convention) so the composition root and
    /// integration tests build identical options. Call after the provider is selected.
    /// </summary>
    public static void ConfigureOptions(DbContextOptionsBuilder options)
    {
        // Apply snake_case naming globally — tables, columns, keys, indexes.
        // EFCore.NamingConventions exposes this as an options-builder extension (10.x API).
        // See ADR (snake_case naming) in docs/DECISIONS.md.
        options.UseSnakeCaseNamingConvention();
    }
}
