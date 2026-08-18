# 0003 — User entity + ASP.NET Core Identity configured with PhoneNumberTokenProvider (no password)

- Status: **Done** (implemented 2026-08-13)
- Date: 2026-08-13
- Related issue/task: PROGRESS.md Phase 0 `0003` — User entity + ASP.NET Core Identity configured with `PhoneNumberTokenProvider` (no password)

## Goal

Introduce the `User` domain entity and stand up **OTP-only** ASP.NET Core Identity: a custom `PhoneNumberTokenProvider`-based token provider wired against our own clean `users` table, with **no password anywhere** and the Domain layer staying free of any ASP.NET Core dependency. When this task is done the `users` table exists (matching `docs/DATABASE.md` §3), Identity's token machinery is resolvable from DI, and an OTP can be generated+verified at the **service level** (proven by an integration test). HTTP endpoints, JWT issuance, and rate-limiting are **out of scope** — they are `0004`.

## Affected modules/layers

- **Domain:** new `User` entity + `CalendarPreference` enum. No new package references (DDD boundary preserved — Domain gains zero ASP.NET Core/EF Core deps).
- **Application:** untouched in this task. The `IOtpSender`/`IOtpVerifier` port interfaces (ARCHITECTURE.md §3) are deferred to `0004`, where they're actually consumed by the auth endpoints.
- **Infrastructure:** gains the `users` table (EF config + `DbSet<User>`), the `Microsoft.Extensions.Identity.Core` package, an Infrastructure-internal `ApplicationUser` adapter, a custom `IUserStore`, and the Identity service registration (using the **built-in** `PhoneNumberTokenProvider`). This is where all ASP.NET Core Identity code lives — **never** in Domain.
- **API:** `Program.cs` calls the new Identity registration (composition-root only). No controllers/endpoints added.
- **Frontend:** untouched.

## Context / constraints driving this plan

This task is a **large change** (new entity + schema change + Identity wiring) per AGENTS.md §1.5, hence this plan and approval gate. Three docs constraints pin the design:

1. `docs/DATABASE.md` §3 defines a **custom `users` table** — columns: `id, phone_number (unique), phone_number_confirmed, display_name, calendar_pref, created_at, deleted_at`. It is **not** Identity's `AspNetUsers` table.
2. AGENTS.md §0/§2: **"no password field anywhere in the system."** A `password_hash`/`security_stamp`-style Identity column set would violate this in spirit (and DATABASE.md in letter).
3. ARCHITECTURE.md §2/§3: Domain must **not** reference ASP.NET Core; Identity's "token provider adapter ... live in Infrastructure"; `IOtpSender`/`IOtpVerifier` are Application-layer ports.

The tension: ASP.NET Core Identity's `UserManager<T>` + `PhoneNumberTokenProvider<T>` normally pull in `IdentityUser<TKey>` and the `AspNetUsers` schema. Resolving that tension cleanly is the core of this task (see Q1).

## Recommended approach

Keep the pure domain `User` in the Domain layer, and host Identity's **token machinery only** in Infrastructure against a custom `IUserStore` that persists to our `users` table. Concretely:

- **Domain `User`** (matches DOMAIN.md §2.1): `Id : Guid`, `PhoneNumber : string` (unique, normalized), `PhoneNumberConfirmed : bool`, `DisplayName : string?`, `CalendarPreference : CalendarPreference`, `CreatedAt : DateTime (UTC)`, `DeletedAt : DateTime?`. Factory `User.Create(phoneNumber)` enforces invariants (non-empty, normalized form). No `PasswordHash`.
- **Infrastructure `ApplicationUser : IdentityUser<Guid>`** — Infrastructure-internal adapter ONLY. **Not** mapped as an EF `DbSet` (so EF never creates `AspNetUsers`); the custom store translates `ApplicationUser` ↔ domain `User`.
- **Custom `IUserStore<ApplicationUser>`** (+ minimal `IUserSecurityStampStore`, `IUserPhoneNumberStore`, `IUserRoleStore` no-op if required by `AddIdentityCore`) implemented over `PomDbContext.Users`, persisting to the clean `users` table. This is what keeps the schema free of the full Identity column set.
- **Built-in OTP token provider** — use ASP.NET Core Identity's built-in `PhoneNumberTokenProvider<ApplicationUser>` **as-is**, registered under `TokenOptions.DefaultPhoneProviderProviderKey` (`"Phone"`). No custom token-provider class is written. Its real validity window is framework-managed (~3–6 minutes via Identity's internal TOTP engine — see Q2); the previously-stated "2-minute" lifetime from ADR-0001 is superseded to match this actual behavior.
- **Service registration** in a new `AddAuth(this IServiceCollection, IConfiguration)` Infrastructure extension: `AddIdentityCore<ApplicationUser>()`, `.AddTokenProvider<PhoneNumberTokenProvider<ApplicationUser>>(TokenOptions.DefaultPhoneProviderProviderKey)`, register the custom `IUserStore<ApplicationUser>` as scoped, and configure `IdentityOptions` (lockout/password disabled — though we carry no password, we keep defaults sane).

`Program.cs` calls `AddInfrastructure(...)` then `AddAuth(...)`. `GET /healthz` stays `200`.

## Files to add/change

Domain (`src/Domain`):
- `Users/User.cs` — entity + `User.Create(...)` factory + `ConfirmPhoneNumber()` method (flips `PhoneNumberConfirmed`). Soft-delete via `DeletedAt` set by later account-deletion task (`0024`); the field exists now.
- `Users/CalendarPreference.cs` — enum `Jalali = 0, Gregorian = 1` (stored as text via EF enum-to-string; matches DATABASE.md `calendar_pref` `'Jalali'|'Gregorian'`).

Infrastructure (`src/Infrastructure`):
- `POM.Infrastructure.csproj` — add `Microsoft.Extensions.Identity.Core` (the `IdentityUser<TKey>`, `UserManager`, token-provider types; .NET 10-aligned `10.x`). No ASP.NET Core *hosting* packages — this is the core Identity abstraction only.
- `Persistence/PomDbContext.cs` — add `DbSet<User> Users => Set<User>();`; override `OnModelCreating(ModelBuilder)` to `ApplyConfigurationsFromAssembly(typeof(PomDbContext).Assembly)` (so `UserConfiguration` is picked up automatically). `ConfigureOptions` unchanged.
- `Persistence/Configurations/Users/UserConfiguration.cs` — `IEntityTypeConfiguration<User>`: table `users` (snake_case already global, but set explicitly for clarity), `id` uuid PK, `phone_number` required + unique index, `phone_number_confirmed` default false, `display_name` nullable, `calendar_pref` required default `Jalali` (enum→string), `created_at` required, `deleted_at` nullable. **Plus the `security_stamp` column if Q1 resolves to storing it** (see Schema changes).
- `Identity/ApplicationUser.cs` — `internal sealed class ApplicationUser : IdentityUser<Guid>`; `UserName`/`PhoneNumber` both carry the phone number; `SecurityStamp` carried from the domain row.
- `Identity/UserStore.cs` — custom store: implements `IUserStore<ApplicationUser>`, `IUserPhoneNumberStore<ApplicationUser>`, `IUserSecurityStampStore<ApplicationUser>` over `PomDbContext.Users`. Create/Get/Update/Delete translate between `ApplicationUser` and domain `User`. Phone uniqueness enforced at the DB index (unique constraint) and surfaced as Identity's `DuplicateUserName`.
- `Identity/DependencyInjection/IdentityServiceCollectionExtensions.cs` — `AddAuth(this IServiceCollection, IConfiguration)`: `AddIdentityCore<ApplicationUser>()` + `.AddTokenProvider<PhoneNumberTokenProvider<ApplicationUser>>(TokenOptions.DefaultPhoneProviderProviderKey)` + custom `IUserStore<ApplicationUser>` registration. The single Identity entry point the API composition root calls. **No custom OTP token-provider class exists** (Q2).
- `Persistence/DependencyInjection/ServiceCollectionExtensions.cs` — unchanged (DbContext only). Identity lives in its own extension to keep concerns separate.

API (`src/Api`):
- `Program.cs` — after `AddInfrastructure`, call `builder.Services.AddAuth(builder.Configuration);`. No other change.

Migration:
- `Persistence/Migrations/<ts>_CreateUsers.cs` (+ `.Designer.cs`, snapshot update) — `create table users (...)` + unique index on `phone_number`; reversible `Down()`.

Tests (`src/tests`):
- `POM.Domain.Tests/Users/UserTests.cs` — unit tests: `User.Create` normalizes/validates phone, defaults `CalendarPreference = Jalali` and `PhoneNumberConfirmed = false`, `ConfirmPhoneNumber()` flips the flag. No infra deps.
- `POM.Infrastructure.Tests` — extend with:
  - `UserMappingTests.cs` — migration applies (existing `DbContextMigrationTests` still green), `users` table created with expected columns/unique index against Testcontainers Postgres.
  - `IdentityOtpTests.cs` — resolve `UserManager<ApplicationUser>` + `SignInManager`-free token path from DI; create a user via the store, `GenerateUserTokenAsync(user, "PhoneNumber", "PhoneNumber")` returns a 6-digit code, `VerifyUserTokenAsync(...)` succeeds with the right code and **fails** with a wrong code. (The real ~3–6 minute framework-managed window from Q2 is not asserted as an exact value — it isn't configurable — only that a valid code verifies and a tampered one does not.)

Root:
- `Pom.sln` — no new project (tests projects already exist from 0001/0002).
- `appsettings.json` / `appsettings.Development.json` — add an `Identity`/`Auth` section only if a config knob is needed (dev-only fixed OTP for tests). Otherwise no change; the OTP validity window is **not configurable** (see Q2) and no lifetime is set in config.

## Schema changes

One migration: `<ts>_CreateUsers`. Creates the `users` table per DATABASE.md §3:

```
users (
  id uuid primary key,                          -- app-assigned Guid.NewGuid() (see Q3)
  phone_number text not null unique,
  phone_number_confirmed boolean not null default false,
  display_name text,
  calendar_pref text not null default 'Jalali',
  created_at timestamptz not null default now(),
  deleted_at timestamptz,
  security_stamp text not null                  -- ONLY if Q1 resolves to storing it
)
```

`__EFMigrationsHistory` already created by the 0002 baseline. **No `AspNetUsers`/`AspNetRoles`/... tables** — the custom store keeps Identity off the schema. This is the whole point of the custom-store approach.

DATABASE.md §3 gets updated to match whatever `security_stamp` decision lands (Q1).

## Risks / open questions

- **Q1 — Identity integration: custom `users` table (custom store) vs. full Identity `AspNetUsers` schema.** This is the pivotal decision.
  - **(a) Custom `users` table + custom `IUserStore` (RECOMMENDED).** Matches DATABASE.md §3 and the "no password field anywhere" rule exactly. Cost: ~1 custom store class + an `ApplicationUser` adapter; the store is small because we only implement OTP-relevant interfaces (no password/role/claim stores beyond what's needed). DATABASE.md stays as written (plus maybe one `security_stamp` column — see below).
  - **(b) Full Identity with `ApplicationUser : IdentityUser<Guid>` mapped as a DbSet, table renamed to `users`.** Far less code, but EF emits the full Identity column set (`password_hash`, `security_stamp`, `two_factor_enabled`, `lockout_*`, `concurency_stamp`, …) inside `users`. That directly contradicts AGENTS.md §0/§2 ("no password field anywhere") and DATABASE.md §3. **Not recommended.**
  - **Recommendation: (a).** It is more code but is the only option consistent with the project's fixed rules. This becomes ADR-0016.
- **Q1b — `SecurityStamp` storage.** Identity token providers require a per-user `SecurityStamp` for TOTP entropy. Options: (a) add a `security_stamp` text column to `users` (small, clean, documented schema addition — DB + DOMAIN.md updated); (b) derive the stamp deterministically from `phone_number + id + a server secret` so no column is needed. **Recommendation: (a)** — explicit, auditable, rotatable on phone-number change. Logged in ADR-0016.
- **Q2 — OTP token lifetime (REVISED).** Use the **built-in** `PhoneNumberTokenProvider<TUser>` as-is. Its validity window is framework-managed and **not precisely configurable** — roughly a 3–6 minute window because Identity's internal TOTP engine (`Rfc6238AuthenticationService`) uses a **hardcoded 3-minute time step** and is `internal`/`sealed`/static with no override point. Subclassing `TotpSecurityStampBasedTokenProvider<TUser>` does **not** allow changing that step (the time-step calculation lives in the sealed `Rfc6238AuthenticationService`, not in the provider subclass). A custom 2-minute provider was therefore **considered and rejected** — it would require reimplementing/rewriting Identity's internal TOTP engine, which is not worth it for shaving a few minutes off an OTP window. **Decision: accept the built-in provider's real ~3–6 minute window.** This supersedes ADR-0001's original "OTP token lifetime: 2 minutes" (ADR-0001 is updated in this task to reflect the actual behavior). Logged as part of ADR-0016 — **no ADR-0017 is created**.
- **Q3 — `Guid` PK assignment.** DATABASE.md says `id uuid` with server `gen_random_uuid()` default. Q3 in the 0002 plan deferred this to 0003. Options: (a) app-side `Guid.NewGuid()` assigned in `User.Create` (recommended — domain controls identity, no round-trip to read a generated id, works before EF `SaveChanges`); (b) Postgres `gen_random_uuid()` default + read-back. **Recommendation: (a)**, app-assigned, EF configured with `ValueGeneratedNever()` for the PK. Logged as a short note in ADR-0016.
- **DDD boundary check (enforced):** grep that `src/Domain` has no reference to `Microsoft.AspNetCore.*` / `Microsoft.Extensions.Identity.*`; only Infrastructure references Identity. `Application` gains no EF Core reference.
- **`AddIdentityCore` minimum store surface:** `AddIdentityCore<ApplicationUser>()` pulls in `SignInManager`? — no, that's `AddIdentity`. `AddIdentityCore` registers `UserManager` + `RoleManager`(optional) + the default token providers and requires at least `IUserStore`. We register only what we implement; if `UserManager` demands an unimplemented store interface, provide a throw/no-op stub and document it. Resolve empirically during implementation.
- **Identity + snake_case convention interaction:** `EFCore.NamingConventions` names whatever DbSet-mapped entities exist. `ApplicationUser` is **not** a mapped DbSet (the custom store reads/writes the domain `User`), so the convention never applies to it and cannot leak Identity column names into a table. Verified by the integration test asserting the `users` table has exactly the documented columns.
- **Migration against the existing baseline:** the empty `InitialCreate` (ADR-0015) is already applied; this migration adds on top. The Testcontainers harness already proves the baseline applies, so a failure here isolates to the User config.

## Acceptance criteria

- [x] `dotnet restore` + `dotnet build --configuration Release` succeed at repo root with zero new warnings.
- [x] New migration `20260813134304_CreateUsers` applies cleanly against a Testcontainers `postgres:18-alpine`; `dotnet ef migrations list` shows `InitialCreate` + `CreateUsers`.
- [x] `users` table exists with exactly the documented columns (incl. `security_stamp`) and a unique index on `phone_number` — asserted by `UserMappingTests`, **and** no `AspNet*` tables are created (`UserMappingTests.No_AspNet_identity_tables_are_created`).
- [x] Domain `User` entity + `CalendarPreference` in `src/Domain`; DDD boundary preserved — `src/Domain/*.csproj` has **no** ASP.NET Core / Identity / EF Core reference (0 `PackageReference`s; grep confirms no forbidden imports).
- [x] `UserManager<ApplicationUser>` + the **built-in** `PhoneNumberTokenProvider` resolve from DI; a phone token is generated and verified at the service level (correct code succeeds, wrong code fails) — `IdentityOtpTests` green.
- [x] `POM.Domain.Tests` (12) + `POM.Infrastructure.Tests` (6) all green; `dotnet test` at root green across all test projects.
- [x] `GET /healthz` still returns `200`.
- [x] **No HTTP auth endpoints added**; **no password field** anywhere (no `PasswordHash` property, no `password_hash` column); **no custom OTP token-provider class** written (built-in used per Q2).
- [x] Docs updated: `DATABASE.md` §3 (`users` table incl. `security_stamp`); `DOMAIN.md` §2.1 (SecurityStamp row + corrected auth note); `DECISIONS.md` ADR-0001 already references the built-in provider's real ~3–6 min non-configurable window, and ADR-0016 gains implementation notes (package split Core+Stores, `InternalsVisibleTo` friend access); `PROGRESS.md` `0003` → `[x]`.
- [x] Plan file marked `Status: Done` at the top.
