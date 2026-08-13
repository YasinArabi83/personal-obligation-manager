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
- Decision: Use ASP.NET Core Identity configured with a custom `PhoneNumberTokenProvider`. Login is phone number + OTP only. No password field anywhere in the system. OTP token lifetime: 2 minutes.
- Consequences: No `PasswordHash` column. Need OTP request rate-limiting to prevent SMS abuse/cost. `User.PhoneNumber` is the Identity username and must be unique.

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
