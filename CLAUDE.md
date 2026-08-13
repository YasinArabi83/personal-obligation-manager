# AGENTS.md — Personal Obligation Manager

> This file is the direct reference for any AI coding agent (Claude Code or similar) working on this repository.
> Source of truth chain: this file overrides `Personal-Obligation-Manager-Spec.md` wherever they conflict. Section 0 lists every point where a final decision replaced an original spec assumption.

---

## 0. Decisions that override the original Spec v1.0

| Topic | Original spec assumption | Final decision |
|---|---|---|
| Auth | Email + Password | **ASP.NET Core Identity + `PhoneNumberTokenProvider`**, phone number + OTP login, no password |
| Primary notification channel | Email (MUST) / SMS (LATER) | **SMS is the primary channel** (Iranian provider, e.g. sms.ir), Email is low priority / not required for MVP |
| Architecture emphasis | Clean/Layered | Explicit **DDD (Domain-Driven Design)** principles apply to every architecture/design decision |
| .NET version | .NET 8/9 | **.NET 10** |
| Frontend | React (preferred) or Angular (acceptable alt.) | **React + TypeScript + Vite only** — no Angular |
| Repository structure | Not specified | **Monorepo** (backend + frontend in the same repo) |
| Backend test framework | Not specified | **xUnit** |
| OTP expiry | Not specified | **2 minutes** |

Any other part of this file or the original spec that references Email/Password auth, Angular, or an older .NET version must be read as superseded by this table.

---

## 1. Before Any Change (Mandatory Checklist)

1. Read `docs/ARCHITECTURE.md` and `docs/DOMAIN.md`.
2. Read `docs/DECISIONS.md` so you don't contradict a prior decision.
3. Read `docs/PROGRESS.md` to see where work last stopped, and open the most recent file in `docs/plans/` if one is relevant to the current task.
4. Confirm the change doesn't break DDD layer boundaries (Domain must never depend on Infrastructure/EF Core/ASP.NET).
5. If the change is large (more than 3 files, a schema change, a new entity, or a change to an aggregate) → **create a new plan file for this task (see section 6) and wait for approval before implementing.**

---

## 2. Fixed Project Rules (never violate)

- **Domain architecture:** Follow DDD — `Obligation` is the Aggregate Root; use Value Objects for concepts like Money and DateRange; raise Domain Events for meaningful state changes (e.g. `ObligationCompleted`); domain logic lives in the Domain layer, never in a bare controller or CRUD service.
- **Auth:** ASP.NET Core Identity, configured with a `PhoneNumberTokenProvider` for OTP. Login is phone number + OTP only — no password field anywhere in the system. OTP token lifetime: **2 minutes**. Every endpoint except `auth/*` must be authorized and filtered by `UserId`.
- **Notifications:** SMS is the default and primary channel, via an Iranian provider behind a swappable interface (`ISmsSender`). Email is optional/low-priority, not a default MVP requirement.
- **Dates:** Always stored as UTC/Gregorian in the database. Convert to Jalali (Persian calendar) only in the presentation layer.
- **Money:** Stored as `bigint` in Rial. Never use `float`/`double` for money.
- **Core entity:** `Obligation` with a `Type` enum (Task, Payment, Document, Subscription, Contract, Debt, Maintenance, Appointment, Custom) plus an `ExtraFields JSONB` column. Don't create a new entity for a new obligation type — extend `ExtraFields` and add validation in the Application layer.
- **External services:** Every external dependency (SMS, storage, email) sits behind a provider-agnostic interface (`ISmsSender`, `IFileStorage`, ...). Never call a third-party SDK directly, due to sanctions/filtering risk in Iran.
- **No AI dependency** in domain logic or any feature, unless explicitly requested in the task.
- **No invented features:** implement only what the issue/task describes. Propose extras as a separate "Suggestion," don't implement them unprompted.

---

## 3. Tech Stack (fixed unless a new decision is logged in DECISIONS.md)

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core (**.NET 10**) |
| Architecture | Modular Monolith + DDD (Domain / Application / Infrastructure / API) |
| ORM | EF Core |
| Database | PostgreSQL 18 |
| Frontend | **React + TypeScript + Vite** |
| Styling | Tailwind CSS (RTL support) |
| Auth | **ASP.NET Core Identity + `PhoneNumberTokenProvider`** (phone + OTP, no password) |
| SMS | Iranian provider (e.g. sms.ir) behind `ISmsSender` |
| Background jobs | Built-in `IHostedService` (or Hangfire if a job dashboard is needed) |
| File storage | **MinIO (S3-compatible)** from day one, behind `IFileStorage` — not local disk |
| Backend test framework | **xUnit** |
| Hosting | Docker Compose on a **domestic (Iranian) VPS**, TLS via a Caddy reverse proxy |
| Repository structure | **Monorepo** (backend + frontend together) |

---

## 4. Domain Model (summary — full source of truth: `docs/DOMAIN.md`)

**Main aggregate:** `Obligation`
- `RecurrenceRule` (0..1)
- `ObligationOccurrence` (0..*, recurring only)
- `Reminder` (0..*)
- `Attachment` (0..*)
- `Tag` (*..*), `Category` (*..1)
- `Person` (0..1, nullable FK) — should-have
- `Asset` (0..1, nullable FK) — should-have

**Supporting MVP entities:** `User`, `Category`, `Tag`, `Attachment`, `ActivityLog`, `NotificationLog`

**Explicitly NOT built in MVP:** Organization, Event as an independent entity, NotificationChannel/Template/Schedule as entities (kept as code config), Contract/Document as separate entities (an Obligation Type / Attachment is sufficient).

---

## 5. Conventions

- Code and database naming: English. Persian only in resource files / UI text.
- Class: PascalCase — variable: camelCase.
- Every new endpoint has a DTO separate from the entity — never expose an entity/aggregate directly.
- Standard error response shape: `{ error: { code, message } }`.
- Every new feature needs at least 1 unit test (xUnit) + 1 integration test.
- Integration tests must explicitly verify data isolation between users (User A must never see or modify User B's Obligation).
- Auth: identify a user by `PhoneNumber` (Identity username = phone number), issue a JWT after OTP verification; rate-limit OTP requests to prevent abuse.

---

## 6. Agent Workflow (mandatory for every task)

**Plans are per-task, not one growing file.** Each task gets its own file at `docs/plans/<NNNN>-<short-slug>.md` (4-digit zero-padded, incrementing — e.g. `0001-obligation-crud.md`, `0002-recurrence-engine.md`). Never append unrelated work to an existing plan file, and never let a single plan file cover more than one task. This keeps each plan short, disposable, and easy to review, instead of one file that grows without bound.

```
1. Analyze requirement        → read the issue/task
2. Read context                → docs/DECISIONS.md, docs/PROGRESS.md, ARCHITECTURE.md, DOMAIN.md, and any still-relevant file in docs/plans/
3. Identify affected modules    → which layers/files are impacted
4. Create docs/plans/NNNN-slug.md → files to add/change, schema changes (if any), risks, acceptance criteria
5. If it's a large change → wait for user approval of the plan file before implementing
6. Implement                    → per rules in sections 2 and 5
7. Write tests                  → unit (xUnit) + integration
8. Run all tests                 → not just the new ones
9. Self-review                  → against the Definition of Done (section 7)
10. Log decisions                → log every meaningful technical decision/trade-off in docs/DECISIONS.md immediately, not later
11. Update docs                  → DOMAIN.md/API.md/DATABASE.md if schema/endpoints changed
12. Mark the plan file done      → add a "Status: Done" line at the top of docs/plans/NNNN-slug.md, and update PROGRESS.md
13. Summarize                    → summary for the commit/PR description
```

Explicit rule: **the agent never starts significant work without reading DECISIONS.md/ARCHITECTURE.md/DOMAIN.md, never mixes two tasks into one plan file, and never silently skips logging an important trade-off in DECISIONS.md.**

---

## 7. Definition of Done

- [ ] Code builds with no new warnings
- [ ] Related tests (unit + integration) pass
- [ ] Migration added and tested (if needed)
- [ ] Docs (`DOMAIN.md`/`API.md`/`DATABASE.md`) updated if schema/endpoints changed
- [ ] `docs/DECISIONS.md` updated if a meaningful technical decision was made
- [ ] This task's plan file in `docs/plans/` marked `Status: Done`
- [ ] `docs/PROGRESS.md` updated
- [ ] No leftover `console.log`/commented-out code
- [ ] Authorization checked (UserId ownership on every query)
- [ ] If the feature touches notifications: SMS is implemented as the primary channel, not Email
- [ ] If the feature touches auth: no password field was added anywhere

---

## 8. Companion Files to Maintain Alongside Code

| File | Role |
|---|---|
| `docs/plans/NNNN-slug.md` | One plan per task, written before implementation starts, marked `Status: Done` when finished. Never shared between tasks. |
| `docs/DECISIONS.md` | Technical decision log (short ADR format) — logged the moment a decision is made |
| `docs/PROGRESS.md` | Task status checklist |
| `docs/ARCHITECTURE.md` | Layers, module boundaries, justification for Modular Monolith + DDD |
| `docs/DOMAIN.md` | Source of truth for the data model (entities, fields, enums, relationships) |
| `docs/API.md` | Endpoint list, request/response shapes, error codes |
| `docs/DATABASE.md` | Full schema, migration policy |
| `docs/SECURITY.md` | Threat model, recovery runbook, secrets management |
| `docs/TESTING.md` | Test strategy, coverage targets |
