# DECISIONS.md — Technical Decision Log

> Every meaningful technical decision or trade-off made during implementation is logged here, **at the moment it's made** — not retroactively. See `AGENTS.md` §6. Newest entries at the bottom. Never edit or delete a past entry to "fix" it — if a decision is reversed, add a new entry that supersedes it and say so.

## Format

```markdown
## ADR-XXXX: <short title>
- Date: <YYYY-MM-DD>
- Status: Accepted | Superseded by ADR-YYYY
- Context: <why this came up>
- Decision: <what was decided>
- Consequences: <trade-offs, follow-up work implied>
```

---

## ADR-0001: Phone number + OTP authentication instead of Email/Password

- Date: 2026-08-13
- Status: Accepted
- Context: Original spec assumed Email + Password auth. This doesn't fit the target user's actual login habits and adds password-reset/email-delivery complexity that's risky given Iran's email service filtering risk.
- Decision: Use ASP.NET Core Identity configured for phone number + OTP login. Login is phone number + OTP only. No password field anywhere in the system. OTP validity is framework-managed via Identity's built-in `PhoneNumberTokenProvider` (a roughly 3–6 minute window from its internal TOTP engine) and is **not** precisely configurable to a fixed value — see ADR-0016 for the full resolution (the original "2 minutes" target is superseded by the framework's real behavior).
- Consequences: No `PasswordHash` column. Need OTP request rate-limiting to prevent SMS abuse/cost. `User.PhoneNumber` is the Identity username and must be unique. The OTP window cannot be tightened to an exact 2 minutes without reimplementing Identity's sealed TOTP engine, so we accept the framework default.

## ADR-0002: SMS as the primary notification channel, Email de-prioritized

- Date: 2026-08-13
- Status: Accepted
- Context: Original spec had Email as MUST and SMS as LATER. In practice, Iranian users are far more reliably reached by SMS than email, and international email providers carry sanctions/filtering risk.
- Decision: SMS (via an Iranian provider such as sms.ir, behind `ISmsSender`) is the default and primary reminder channel. Email is optional/low priority, not required for MVP.
- Consequences: `Reminder.Channel` default is `Sms`. Email sending infrastructure can be deferred or built minimally.

## ADR-0003: Explicit DDD emphasis in architecture

- Date: 2026-08-13
- Status: Accepted
- Context: Original spec described a generic Clean/Layered architecture.
- Decision: Architecture explicitly follows Domain-Driven Design principles — `Obligation` as Aggregate Root, Value Objects for Money/DateRange-like concepts, Domain Events for meaningful state transitions, domain logic isolated from Infrastructure/API layers.
- Consequences: Domain layer must have zero dependency on EF Core/ASP.NET Core. Slightly more upfront structure than a plain CRUD layered approach, in exchange for a domain model that stays coherent as obligation types grow.

## ADR-0004: .NET 10 as the backend runtime

- Date: 2026-08-13
- Status: Accepted
- Context: Original spec assumed .NET 8/9.
- Decision: Target .NET 10.
- Consequences: None expected beyond normal SDK/tooling version pinning in CI.

## ADR-0005: React-only frontend, Angular dropped

- Date: 2026-08-13
- Status: Accepted
- Context: Original spec offered React as preferred with Angular as an acceptable alternative, deferring the final call.
- Decision: Frontend is React + TypeScript + Vite only.
- Consequences: No Angular tooling/scaffolding is ever created. Frontend conventions in `AGENTS.md`/`docs/ARCHITECTURE.md` should assume React exclusively.

## ADR-0006: Monorepo structure

- Date: 2026-08-13
- Status: Accepted
- Context: Original spec's repository structure sketch didn't commit to monorepo vs. polyrepo.
- Decision: Backend and frontend live in the same repository.
- Consequences: CI pipeline needs to handle both a .NET build/test job and a frontend build/test job from one repo; path-based triggers may be worth adding later to avoid running both on every change.

## ADR-0007: xUnit as the backend test framework

- Date: 2026-08-13
- Status: Accepted
- Context: Original spec didn't pin a specific .NET test framework.
- Decision: Use xUnit for all backend unit and integration tests.
- Consequences: None — standard choice, wide tooling support (including Testcontainers for Postgres integration tests per spec §32).

## ADR-0008: Plans are per-task files, not one growing PLAN.md

- Date: 2026-08-13
- Status: Accepted
- Context: A single `docs/PLAN.md` accumulated across every task would grow unbounded and become hard to review or trust as current.
- Decision: Each task that requires a written plan gets its own file at `docs/plans/NNNN-short-slug.md` (4-digit zero-padded, incrementing), marked `Status: Done` at the top when the task is complete instead of being deleted.
- Consequences: `docs/plans/` accumulates a readable history of past task plans. Agents must check for an existing relevant plan file before starting related work rather than assuming a single canonical plan location.

## ADR-0009: Deploy target is a domestic (Iranian) VPS

- Date: 2026-08-13
- Status: Accepted
- Context: Original spec left the hosting target open (foreign VPS vs. domestic provider like ArvanCloud), to be decided later. `docs/SECURITY.md` and `docker-compose.yml` were left provider-agnostic pending this decision.
- Decision: Production deployment targets a domestic Iranian VPS. TLS termination is handled locally on that VPS (Caddy, automatic HTTPS via Let's Encrypt) rather than assuming a foreign CDN/edge. Off-site backups must be stored somewhere other than the same VPS/provider account to survive a single-provider outage.
- Consequences: `docker-compose.yml` gains a reverse-proxy service for TLS. `docs/SECURITY.md`'s backup/recovery runbook is finalized around this target instead of staying generic. Every external dependency (SMS, storage) still stays provider-agnostic per `AGENTS.md` §2 — the VPS choice doesn't change that rule, since domestic providers can also change or become unavailable.

## ADR-0010: PostgreSQL version pinned to 18

- Date: 2026-08-13
- Status: Accepted
- Context: `docker-compose.yml` and `docs/DATABASE.md` needed a concrete image tag rather than "latest" (which would make builds non-reproducible over time).
- Decision: Pin `postgres:18-alpine` everywhere Postgres is referenced (Docker Compose, Testcontainers integration tests, docs).
- Consequences: Any future version bump is itself a decision that needs a new ADR and a coordinated update across `docker-compose.yml`, `docs/DATABASE.md`, and `docs/TESTING.md`.

## ADR-0011: MinIO included from day one, not deferred behind local disk

- Date: 2026-08-13
- Status: Accepted
- Context: An earlier draft of `AGENTS.md` suggested "local disk for MVP, S3-compatible storage later" as a simplification. This would mean building `IFileStorage` twice (once for local disk, once for MinIO) and migrating stored files later.
- Decision: MinIO is provisioned in `docker-compose.yml` from the first infrastructure task, and `IFileStorage`'s only implementation is the MinIO/S3-compatible adapter — no local-disk-only interim implementation.
- Consequences: One `IFileStorage` implementation instead of two; no later data-migration task needed for attachments. Slightly more setup in Phase 0 (a MinIO container + bucket bootstrapping) in exchange for not building throwaway code.

## ADR-0012: Central Package Management deferred (per-project package versions for now)

- Date: 2026-08-13
- Status: Accepted
- Context: Task 0001 considered `Directory.Packages.props` (Central Package Management) so all projects pin shared package versions in one file. At scaffolding time there is exactly one project with package references (the xUnit test project), so the file would be pure ceremony.
- Decision: Skip Central Package Management in 0001; each project pins its own package versions. EF Core packages arrive in 0002 — if version duplication across projects becomes real (i.e. more than one project shares the same EF/Npgsql packages), revisit with a new ADR that supersedes this one.
- Consequences: Possible version drift between projects in the short term; mitigated by the fact that nearly all package references will live in Infrastructure + test projects only. Domain/Application stay package-free by rule.

## ADR-0013: Classic `.sln` solution format, not `.slnx`

- Date: 2026-08-13
- Status: Accepted
- Context: `dotnet new sln` on the .NET 10 SDK defaults to the new XML-based `.slnx` format. The 0001 plan and `src/Api/Dockerfile` reference `Pom.sln`; the `.slnx` default only surfaced when the first Docker build failed (`COPY Pom.sln` not found).
- Decision: Keep the classic `Pom.sln` (created via `dotnet new sln --format sln`), matching the plan and Dockerfile verbatim.
- Consequences: Broadest tooling compatibility (older CLI/IDE/CI tooling that predates `.slnx` support). `.slnx` remains a possible future migration; if adopted, the Api Dockerfile's `COPY Pom.sln` / `dotnet restore Pom.sln` lines must change with it.

## ADR-0014: snake_case DB naming via EFCore.NamingConventions, applied on the options builder

- Date: 2026-08-13
- Status: Accepted
- Context: `docs/DATABASE.md` §2 mandates snake_case tables/columns/keys/indexes. Doing this manually per entity (`.ToTable("x").HasColumnName("y")` everywhere) is verbose and easy to forget as entities grow (User in 0003, Obligation in 0006, …). Two options were considered in the 0002 plan: a convention package, or manual naming. Q1 chose the package.
- Decision: Use the `EFCore.NamingConventions` package and call `options.UseSnakeCaseNamingConvention()` once in `PomDbContext.ConfigureOptions` — the single options-tuning point shared by the DI composition root and integration tests. This names every entity's tables/columns/keys/indexes as snake_case automatically.
- Consequences: No manual `ToTable`/`HasColumnName` needed for naming. Note the package's 10.x API exposes the extension on `DbContextOptionsBuilder`, **not** on `ModelBuilder` — so the convention is applied at options-build time (`ConfigureOptions`), not in `OnModelCreating`. If an entity ever needs a non-snake_case name (legacy interop), that becomes a deliberate per-entity override in its `IEntityTypeConfiguration`.

## ADR-0015: Empty `InitialCreate` baseline migration before any business table

- Date: 2026-08-13
- Status: Accepted
- Context: Task 0002 wires the EF Core pipeline (DbContext, Npgsql provider, snake_case convention, DI, Testcontainers test) but introduces no entities (User is 0003). Q2 in the 0002 plan asked whether to ship a truly empty first migration or defer the first migration to 0003. Shipping the empty baseline de-risks 0003: if the first real migration fails, the cause is the entity config, not the pipeline wiring — and the Testcontainers test already proves the empty baseline applies cleanly against real Postgres.
- Decision: Ship `20260813115423_InitialCreate` now as an empty baseline (empty `Up()`/`Down()`, provider-managed `__EFMigrationsHistory` table is created by EF). 0003's first migration will add the `users` table on top of this baseline.
- Consequences: One extra migration in history. `dotnet ef migrations list` shows `InitialCreate`. A production database applied against this baseline has only the migrations history table until 0003. Do **not** delete or squash this baseline later — `AGENTS.md`/DATABASE.md §5 forbids editing applied migrations; a later squash would require its own ADR.

## ADR-0016: Custom Identity user store over a clean `users` table, app-assigned Guid PK, and the built-in `PhoneNumberTokenProvider`

- Date: 2026-08-13
- Status: Accepted
- Context: Task 0003 (User entity + ASP.NET Core Identity, OTP-only) had to reconcile three fixed constraints: (1) `docs/DATABASE.md` §3 defines a custom `users` table (`id, phone_number (unique), phone_number_confirmed, display_name, calendar_pref, created_at, deleted_at`), not Identity's `AspNetUsers` schema; (2) AGENTS.md §0/§2 forbids a password field anywhere; (3) ARCHITECTURE.md §2/§3 keeps all ASP.NET Core Identity code in Infrastructure and the Domain layer free of it. The default Identity integration (`ApplicationUser : IdentityUser<Guid>` mapped as a DbSet) would emit the full Identity column set (`password_hash`, `two_factor_enabled`, `lockout_*`, `concurrency_stamp`, …) into `users`, violating constraints (1) and (2). Three sub-decisions fell out of this, recorded together because they are inseparable for 0003.
- Decision:
  - **Custom user store (Q1).** Keep the domain `User` entity pure in the Domain layer. In Infrastructure, host an internal `ApplicationUser : IdentityUser<Guid>` adapter that is **not** mapped as an EF DbSet, and implement a custom `IUserStore<ApplicationUser>` (+ `IUserPhoneNumberStore`, `IUserSecurityStampStore`) over `PomDbContext.Users` persisting to the clean `users` table. EF therefore never creates `AspNetUsers`/`AspNetRoles`/… — no password column, no lockout columns, exactly the documented schema. The store implements only the OTP-relevant interfaces.
  - **SecurityStamp column (Q1b).** Identity's TOTP token providers need a per-user `SecurityStamp` for entropy. Add a single explicit `security_stamp text not null` column to `users` (rather than deriving it deterministically from phone+id+secret) — explicit, auditable, and rotatable on phone-number change. `DATABASE.md` §3 and `DOMAIN.md` §2.1 are updated to match.
  - **App-assigned Guid PK (Q3).** The `id` is assigned application-side (`Guid.NewGuid()` in the `User.Create` domain factory), with EF configured `ValueGeneratedNever()` on the PK — domain controls identity, no round-trip to read a generated id, works before `SaveChanges`. Supersedes the DATABASE.md §3 note that deferred PK assignment from 0002 to 0003.
  - **OTP token provider (Q2, supersedes the "2-minute lifetime" in ADR-0001).** Use Identity's **built-in** `PhoneNumberTokenProvider<ApplicationUser>` as-is, registered under `TokenOptions.DefaultPhoneProviderProviderKey` (`"Phone"`). **No custom OTP token-provider class is written.** A custom provider intended to enforce an exact 2-minute lifetime was considered and **rejected**: Identity's internal TOTP engine (`Rfc6238AuthenticationService`) is `internal`/`sealed`/static, uses a **hardcoded 3-minute time step** with no override point, and subclassing `TotpSecurityStampBasedTokenProvider<TUser>` does **not** allow changing that step (the time-step calculation lives in the sealed engine, not the provider subclass). The real validity window is therefore framework-managed (~3–6 minutes) and **not precisely configurable** without reimplementing Identity's TOTP engine — not worth it to shave minutes off an OTP. We accept the framework default. **No separate ADR-0017 is created** for the token provider; this Q2 resolution is folded into ADR-0016.
- Consequences: More code than the default Identity mapping (one custom store class + an adapter), but it is the only option consistent with the project's fixed "no password field" and custom-`users`-table rules. `EFCore.NamingConventions` (ADR-0014) does not touch `ApplicationUser` because it is not a mapped DbSet, so no Identity column names leak into the schema. The exact OTP lifetime is no longer a tunable we control; any future requirement for a precise sub-3-minute window would require a new ADR and a from-scratch TOTP implementation. This ADR supersedes the "OTP token lifetime: 2 minutes" wording in ADR-0001.
  - **Implementation notes (landed 2026-08-13 / task 0003):** (a) The Identity package is split across two references — `Microsoft.Extensions.Identity.Core` (UserManager + token providers + the `IUserStore` interfaces) **and** `Microsoft.Extensions.Identity.Stores` (`IdentityUser<TKey>` itself); Core alone does not contain `IdentityUser<>`. (b) The custom `UserStore` needs to sync `ApplicationUser` state onto the domain `User` during `UpdateAsync`. Rather than invent artificial domain methods, the domain `User` exposes auth-relevant fields with `internal` setters and the `POM.Domain` assembly grants `InternalsVisibleTo("POM.Infrastructure")` — the persistence adapter is a "friend" of the domain. The public domain API stays clean; only Infrastructure (and its test project) can mutate those fields. The phone token provider is registered under `TokenOptions.DefaultPhoneProvider` (the framework's `"Phone"` key); OTP generation/verification uses purpose `"PhoneNumber"`.

## ADR-0017: OTP auth endpoints — JWT + hashed/rotated refresh tokens, hybrid rate limiting, and the refresh-token/refreshToken-shape deviations

- Date: 2026-08-13
- Status: Accepted
- Context: Task 0004 builds the three auth endpoints (`otp/request`, `otp/verify`, `refresh`) on top of the Identity plumbing from 0003. It introduced several decisions that the 0004 plan either left open or resolved in a way that turned out to need adjusting during implementation. (Note: ADR-0016 said "no ADR-0017 is created for the token provider" — that was about the *OTP* token provider in 0003. This ADR-0017 covers the *access/refresh-token* and *endpoint* decisions of 0004, a different subject.)
- Decision:
  - **Refresh token = Infrastructure persistence model, not a Domain aggregate (plan Q3).** `RefreshToken` lives in `POM.Auth.Jwt` (Infrastructure) and maps to a `refresh_tokens` table. A refresh token is an auth/infra concern, not domain behavior, so the Domain stays free of auth dependencies. Confirmed by grep: zero `Microsoft.AspNetCore.*`/`Microsoft.EntityFrameworkCore`/JWT usings in Domain or Application.
  - **Token lifetimes (plan Q8).** Access JWT = 15 min, refresh = 30 days, from configuration (`Jwt:AccessLifetimeMinutes`, `Jwt:RefreshLifetimeDays`) with those defaults. Signing key, issuer, audience all read from `Jwt:*` config — never hardcoded.
  - **Refresh-token rotation + reuse detection (plan Q1-a).** Refresh tokens are random URL-safe bytes stored only as a SHA-256 hash; tokens from one login share a `family_id`. On `refresh`, the consumed token is revoked and a new one issued in the same family. Replaying an already-consumed token revokes the **entire family** and rejects — so a stolen-and-rotated token cannot be reused after the legitimate rotation.
  - **Hybrid rate limiting (plan Q2).** The built-in `AddRateLimiter` provides a coarse per-IP fixed window (30/min) on `auth/*` (it cannot partition on a JSON-body phone number); the in-memory `IOtpRateLimiter` enforces the per-phone windows from SECURITY.md §2 (3 OTP requests / 10 min, 5 failed verifies / 1 h) inside the application. In-memory only — single-VPS MVP, no Redis.
  - **`JwtBearer` validation + per-IP limiter live in the API layer**, not Infrastructure. These are ASP.NET Core hosting/middleware concerns backed by the Web SDK shared framework; `AddPomApiAuthentication` in the API composition root registers them. The token *issuance* (`JwtTokenService`) stays in Infrastructure behind the `ITokenService` port. This keeps the Architecture §2 composition-root exception honest.
  - **Deviation from plan Q7 — find-or-create happens at OTP *request* time (in `OtpSender`), not at verify time (in `OtpVerifier`).** Identity's TOTP is derived from the user's `SecurityStamp`, so a code can only be generated — and later verified — against a *persisted* stamp. A transient in-memory user would get a throwaway stamp that never matches the row created later. Creating the user row at request is harmless: the row is inert until `OtpVerifier` flips `PhoneNumberConfirmed` on a successful verify, and the per-phone rate limiter bounds row creation. A successful OTP remains the only thing that *confirms* a number.
  - **Deviation from plan Q4-a — the refresh token IS returned in both `verify` and `refresh` responses.** The plan/Q4-a said "no `refreshToken` field in either body," but refresh-token rotation is unusable (and untestable) if the newly-issued token is never returned to the client: after one `refresh` the client would hold a dead token. Standard token-rotation practice returns the new refresh token. So `verify → {accessToken, expiresAt, refreshToken, user}` and `refresh → {accessToken, expiresAt, refreshToken}`. `docs/API.md` §2 is updated to match.
  - **Signing key in dev.** A clearly-marked dev placeholder key is committed in `appsettings.Development.json` (mirroring the committed dev DB password); production must override `Jwt:SigningKey` via env/secret. `JwtTokenService` and `AddPomApiAuthentication` both throw on startup if the key is missing or under 32 bytes.
- Consequences: Full OTP login → JWT issuance → rotating refresh → reuse-revocation flow works end-to-end and is covered by unit tests (rotation, reuse detection, rate-limit windows, OTP find-or-create) and HTTP integration tests (request → verify → refresh → rotate → replay, per-phone rate isolation). Rate-limit state is process-local (lost on restart) — acceptable for single-VPS MVP; a move to Redis/multi-node would need a new ADR. The committed dev signing key must be replaced in every real deployment. `docs/API.md`, `docs/DATABASE.md` (`refresh_tokens`), and `docs/SECURITY.md` are updated in this task.

## ADR-0018: Swashbuckle Swagger UI, Development-only, with a JWT Bearer scheme

- Date: 2026-08-13
- Status: Accepted
- Context: The API is a minimal-API surface; a Swagger UI is wanted to exercise the auth (and later obligation) endpoints during development. .NET 10 ships built-in OpenAPI document generation (`Microsoft.AspNetCore.OpenApi` + `MapOpenApi`), but that serves only the JSON document, not an interactive UI.
- Decision: Add `Swashbuckle.AspNetCore` 10.x for both document generation and the Swagger UI. Registration lives in a dedicated `SwaggerExtensions.AddPomSwagger` (matching the project's per-concern composition style) and `UseSwagger`/`UseSwaggerUI` are gated behind `IsDevelopment()` — the UI is never exposed in production (docs/SECURITY.md). A JWT Bearer security scheme (`SecuritySchemeType.Http`, scheme `bearer`, JWT) is registered so the UI's **Authorize** button can hold the access token from `otp/verify` and attach `Authorization: Bearer …` to subsequent calls. No separate plan file was created — this is a 2-file dev-tooling change (no schema/entity/aggregate change), below the AGENTS.md §1 "large change" threshold.
- Consequences: Swagger UI available at `/swagger` in Development. Swashbuckle 10.x targets `Microsoft.OpenApi` 2.0, whose API differs materially from 1.x: model types moved out of `Microsoft.OpenApi.Models` into the flat `Microsoft.OpenApi` namespace (`OpenApiInfo`, `OpenApiSecurityScheme`, `ParameterLocation`, `SecuritySchemeType`, …), `OpenApiSecurityScheme` no longer carries a `Reference` property (use `OpenApiSecuritySchemeReference` instead), and Swashbuckle's `AddSecurityRequirement` takes a `Func<OpenApiDocument, OpenApiSecurityRequirement>` factory rather than a plain instance. Future OpenAPI-related work in this repo must follow the 2.0 API.

## ADR-0019: Nullable category reference until taxonomy tables ship

- Date: 2026-08-23
- Status: Accepted
- Context: The `Obligation` model includes a category relationship, but the `categories` table is deliberately scheduled for task 0007. A non-null foreign key in task 0006 would either create an invalid migration or pull taxonomy scope into the aggregate-core task.
- Decision: Task 0006 creates `obligations.category_id` as nullable and does not create a foreign-key constraint. Task 0007 will introduce `categories` and add the nullable foreign key and ownership validation.
- Consequences: Core obligations can be persisted without a category during the first slice. Task 0007 must add the categories table and its FK/index deliberately, and API validation must prevent cross-user category references.

## ADR-0020: Category defaults and referenced-category deletion policy

- Date: 2026-08-23
- Status: Accepted
- Context: Task 0007 needed deterministic system categories and a safe behavior when a user category is still referenced by an obligation.
- Decision:
  - Seed six stable, globally visible defaults (`Bills`, `Subscriptions`, `Documents`, `Maintenance`, `Appointments`, `Personal`) with fixed UUIDs. The migration inserts them and the infrastructure `DefaultCategorySeeder` is idempotent for repeated startup/test provisioning.
  - User-facing reads return defaults plus the current user's categories. System defaults and other users' categories are treated as not found for mutations to avoid existence leaks.
  - A referenced user category cannot be deleted; the API returns `409 conflict`. The obligation FK uses `ON DELETE RESTRICT`, so no obligation is cascaded or silently orphaned.
  - Category names/icons are trimmed; names and icons are capped at 100 characters. No uniqueness constraint is imposed in this slice.
- Consequences: Defaults are stable across environments and safe to re-run. Users must reassign obligations before deleting a referenced category; later UX can add reassignment explicitly.
