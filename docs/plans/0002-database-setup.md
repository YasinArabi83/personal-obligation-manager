# 0002 — Database setup: EF Core + DbContext + first (empty) migration + Postgres wiring

- Status: Done
- Date: 2026-08-13
- Related issue/task: PROGRESS.md Phase 0 `0002` — Database setup: EF Core + DbContext + first empty migration + Postgres wired in `docker-compose.yml`

## Goal

Stand up the persistence stack: add EF Core + Npgsql to the Infrastructure layer, create the application `DbContext` (no entities yet — `User` is `0003`), wire it into the API composition root, and generate the first migration (`InitialCreate`, intentionally empty). Verify the whole chain end-to-end against a real disposable Postgres via Testcontainers. Postgres itself is already defined in `docker-compose.yml` (ADR-0010, `postgres:18-alpine`) — this task only adds the application-side wiring, connection config, and the migration tooling.

When this task is done: `dotnet ef migrations add …` works, `dotnet ef database update` applies cleanly against a Testcontainers Postgres, the API resolves a `DbContext` from DI, and `GET /healthz` still returns `200`. No business entities or tables exist yet.

## Affected modules/layers

- **Domain:** untouched (zero new packages/references — DDD boundary preserved, ARCHITECTURE.md §2).
- **Application:** untouched in this task. (Application services will later depend on repository ports, not on the `DbContext`; the `DbContext` stays internal to Infrastructure. No port interface is added yet — deferred to `0006` with `IObligationRepository`.)
- **Infrastructure:** gains EF Core + Npgsql packages, the `PomDbContext`, an `AddInfrastructure` DI extension, and the EF migration files.
- **API:** gains a `POM.Infrastructure` project reference (composition-root only), and calls `AddInfrastructure` in `Program.cs`. No controller changes.
- **Tests:** new xUnit project `src/tests/POM.Infrastructure.Tests` with the Testcontainers Postgres harness + one integration test (migration applies, `DbContext` can open a connection).
- **Frontend:** untouched.

## Files to add/change

Backend — Infrastructure (`src/Infrastructure`):
- `POM.Infrastructure.csproj` — add package references (versions pinned per ADR-0012, central package management still deferred):
  - `Microsoft.EntityFrameworkCore` (EF Core core, .NET 10-aligned version)
  - `Npgsql.EntityFrameworkCore.PostgreSQL` (Npgsql EF Core provider)
  - `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets="all"`, design-time tooling only — not a runtime dep)
  - `EFCore.NamingConventions` (snake_case table/column naming — see open question Q1)
- `Persistence/PomDbContext.cs` — `public class PomDbContext : DbContext`. Constructor `(DbContextOptions<PomDbContext>)`. **No `DbSet<>` properties yet.** Overrides `OnModelCreating` to call `modelBuilder.UseSnakeCaseNamingConvention()` (if Q1 resolved to the package). Sets `HasDefaultSchema` only if needed (leave default). Comments note that entity configurations land with their tasks (`User` → `0003`, `Obligation` → `0006`).
- `Persistence/DependencyInjection/ServiceCollectionExtensions.cs` — `public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)`: reads `ConnectionStrings:Default`, registers `AddDbContext<PomDbContext>(o => o.UseNpgsql(connStr, npg => npg.MigrationsAssembly(typeof(PomDbContext).Assembly.FullName)))`. This is the only Infrastructure type the API layer references.
- `Persistence/migrations/` — generated `*_InitialCreate.cs` (empty `Up`/`Down` body — provider-managed `__EFMigrationsHistory` only) + `PomDbContextModelSnapshot.cs`.

Backend — API (`src/Api`):
- `POM.Api.csproj` — add `<ProjectReference Include="..\Infrastructure\POM.Infrastructure.csproj" />`. Comment: this is the documented composition-root exception (ARCHITECTURE.md §2); controllers never touch Infrastructure types.
- `Program.cs` — call `builder.Services.AddInfrastructure(builder.Configuration);` (replacing the `// Infrastructure DI registration will be wired here in task 0002` placeholder).
- `appsettings.Development.json` — add `"ConnectionStrings": { "Default": "Host=127.0.0.1;Port=5432;Database=pom;Username=pom;Password=dev" }` for local `docker-compose up postgres` dev. Production stays on the `ConnectionStrings__Default` env var already present in `docker-compose.yml`.
- `appsettings.json` — add an empty `"ConnectionStrings": { "Default": "" }` placeholder so the key always resolves (production overrides via env var).

Backend — Tests (`src/tests`):
- `POM.Infrastructure.Tests/POM.Infrastructure.Tests.csproj` — xUnit, references `POM.Infrastructure`. Packages: `Testcontainers.Postgres` (+ `Testcontainers` transitively), `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`. Disable `TreatWarningsAsErrors` if Testcontainers' source generators warn (only if a real warning surfaces — otherwise leave the repo default).
- `POM.Infrastructure.Tests/PostgresFixture.cs` — `IAsyncLifetime` fixture that starts a `postgres:18-alpine` container, exposes its connection string, and disposes it. Shared per-test-class (`ICollectionFixture<…>`).
- `POM.Infrastructure.Tests/DbContextMigrationTests.cs` — one integration test: build `PomDbContext` against the Testcontainers connection, run `await db.Database.MigrateAsync()`, assert `db.Database.CanConnectAsync()` is true. This exercises the provider, the migration, and the snake_case config together.

Root:
- `Pom.sln` — add the new `POM.Infrastructure.Tests` project (and a `tests` solution-folder entry already exists from `0001`).
- `env.example` — no change (`POSTGRES_*` already present; the dev connection string is hardcoded local-only and not a secret worth templating).
- `docker-compose.yml` — **no change** (Postgres service + `ConnectionStrings__Default` env mapping already added in `0001` per its plan; ADR-0010 pins the image).

Tooling:
- Ensure `dotnet-ef` tool is available (`dotnet tool install --global dotnet-ef` if missing; it is not committed). Document the command in `docs/TESTING.md` / a short `docs/DATABASE.md` note? — no, keep docs changes to `DATABASE.md` migration-policy confirmation (already documents EF migrations).

## Schema changes

One migration: `*_InitialCreate` — **empty body** (`Up`/`Down` are no-ops; the `__EFMigrationsHistory` table is created automatically by the Npgsql provider on first `Migrate`, not emitted in the migration body). This is the deliberate baseline that locks in the migration pipeline before the first real table (`users`) arrives in `0003`. No business tables, no columns, no enums introduced in this task.

## Risks / open questions

- **Q1 — snake_case naming: package vs. manual.** `DATABASE.md` §2 mandates snake_case tables/columns. Options: (a) `EFCore.NamingConventions` community package (`UseSnakeCaseNamingConvention()`) — one line, covers all entities forever; (b) manual `ToTable("…")`/`HasColumnName("…")` per config. **Recommendation: (a)** — NuGet is accessible from Iran, the package is small and widely used, and manual naming is a perpetual source of drift/bugs. This is a real trade-off (one extra dependency in Infrastructure) and gets logged as an ADR during implementation. *(Need confirmation if reviewer prefers the no-extra-dependency manual route.)*
- **Q2 — empty baseline migration worth it?** Some prefer skipping the empty `InitialCreate` and starting migrations at `0003` (`users` table) so the first migration is non-trivial. PROGRESS.md explicitly lists "first empty migration" for `0002`, and an empty baseline now de-risks `0003`: it proves `dotnet ef` + Npgsql + Testcontainers all work before any business entity exists, so a `0003` failure is clearly the entity config, not the pipeline. **Recommendation: keep the empty baseline.**
- **Q3 — `Guid` PK default.** Npgsql maps `Guid` → `uuid` and defaults to `gen_random_uuid()` server-side. No entity uses a `Guid` PK until `0003`, so nothing to configure now — but the chosen convention (let EF/Npgsql generate the default, not app-side `Guid.NewGuid()`) should be settled in `0003`, not here. Noted here only so it's not a surprise.
- **DDD boundary check:** Adding the `POM.Infrastructure` reference to `POM.Api.csproj` is the one sanctioned exception (composition root). Controllers/Application services must **not** take a `PomDbContext` dependency — only repositories/interfaces (later). A code-review gate in this task: grep that nothing under `src/Api/Controllers` or `src/Application` references `PomDbContext` or `Microsoft.EntityFrameworkCore`.
- **`TreatWarningsAsErrors` + Testcontainers:** Testcontainers occasionally triggers analyzer warnings. If the build breaks on a warning originating in test-package-generated code, scope a `<WarningsNotAsErrors>` entry to the test project only (do **not** weaken the repo-wide setting). Resolve empirically during implementation.
- **EF Core package versions on .NET 10:** pin to the EF Core release line that ships for .NET 10 (e.g. `10.x`). Verify `dotnet restore` resolves; if a version isn't published yet on the runner, this surfaces immediately as a restore failure — the natural blocking point.

## Acceptance criteria

- [ ] `dotnet restore` + `dotnet build --configuration Release` succeed at repo root with zero warnings (or test-project-only `WarningsNotAsErrors` exceptions, justified in an ADR).
- [ ] `dotnet ef migrations list` (in `src/Infrastructure`) shows `00000000000000_InitialCreate` (timestamp prefix).
- [ ] `dotnet ef database update` applies `InitialCreate` cleanly against a local Postgres (`docker compose up postgres` + dev connection string) — verified by the Testcontainers integration test in CI.
- [ ] New `POM.Infrastructure.Tests` project: the `DbContextMigrationTests` test passes (migration applies, `CanConnect` is true) against a disposable `postgres:18-alpine` container.
- [ ] `POM.Domain.Tests` sanity test still passes; `dotnet test` at root is green across both test projects.
- [ ] `GET /healthz` still returns `200 {"status":"ok"}` with the new DI wiring in place.
- [ ] DDD boundary preserved: Domain has zero new references; Application has zero references to EF Core / `PomDbContext`; only `Program.cs` (composition root) references `POM.Infrastructure`.
- [ ] No business entities/tables introduced (`User`, `Obligation`, etc. stay in `0003`+).
- [ ] Docs updated: `DATABASE.md` migration-policy section confirmed accurate (no schema added, so no §3 change needed — add a one-line note that the `InitialCreate` baseline exists); `DECISIONS.md` gets the Q1 (snake_case) ADR and the Q2 (empty baseline) ADR; `PROGRESS.md` `0002` → `[x]`.
- [ ] Plan file marked `Status: Done` at the top once implementation + tests are green.
