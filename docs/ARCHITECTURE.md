# ARCHITECTURE.md — Personal Obligation Manager

> Explains the system's shape: why Modular Monolith + DDD, where the layer boundaries are, and what rules an agent must never break when adding code. Pairs with `docs/DOMAIN.md` (what the model is) and `AGENTS.md` (how to work).

---

## 1. Why Modular Monolith, not Microservices

Team is a solo developer + AI agent, user scale is a few thousand in year one, and the priority is development speed and simple deployment — not immediate horizontal scale. A Modular Monolith gives clear module boundaries (Obligations, Users, Notifications, Files) from day one, while keeping deployment to a single Docker Compose stack. A module can be extracted into its own service later **if a real need appears**, not preemptively.

## 2. Layers

```
Domain          Entities, Value Objects, domain logic, domain events.
                 No dependency on EF Core, ASP.NET Core, or any infrastructure package.

Application     Use cases / application services, port interfaces
                 (ISmsSender, IFileStorage, IOtpSender, ICurrentUser, ...).
                 Depends only on Domain.

Infrastructure  EF Core (DbContext, configurations, migrations), PostgreSQL,
                 concrete ISmsSender/IFileStorage implementations, background job runners.
                 Depends on Domain + Application (implements Application's interfaces).

API             ASP.NET Core controllers/minimal API, DTOs, request validation, auth wiring.
                 Depends on Application (and Infrastructure only for composition-root/DI wiring
                 in Program.cs — controllers never call Infrastructure types directly).

Frontend        React + TypeScript SPA, calls the API over HTTP. Fully separate project,
                 lives in the same monorepo.
```

### Dependency rule (never break this)

```
Domain  ←  Application  ←  Infrastructure
                ↑
               API
```

Arrows point toward the thing being depended on. Domain depends on nothing. Application depends only on Domain. Infrastructure and API both depend on Application; Infrastructure and API never depend on each other directly except through DI registration in the composition root (`Program.cs`).

**If a change requires Domain to reference `Microsoft.EntityFrameworkCore` or any ASP.NET Core namespace, that change is wrong — stop and re-plan.**

## 3. DDD building blocks used in this project

- **Aggregate Root:** `Obligation` is the aggregate root for its cluster (`RecurrenceRule`, `ObligationOccurrence`, `Reminder`, `Attachment`, tag links). All writes to these go through the `Obligation` aggregate, not directly against child tables.
- **Value Objects:** used for concepts without identity — e.g. a `Money` value object wrapping the `bigint` Rial amount + currency, a `DateRange`/`RecurrencePattern`-style value object where useful. Value Objects are immutable and compared by value, not by Id.
- **Domain Events:** raised for meaningful state transitions (`ObligationCompleted`, `ObligationBecameOverdue`, `OccurrenceGenerated`). Domain Events are dispatched by Application services after a successful persistence, and are what triggers reminder scheduling / activity log writes — keeping that logic out of the Domain layer itself while still keeping "what happened" domain-defined.
- **Repositories:** one repository per aggregate root (`IObligationRepository`), not one per table. Child entities (`Reminder`, `Attachment`, etc.) are only reachable through their aggregate root.
- **Ports & Adapters at the edges:** `ISmsSender`, `IFileStorage`, `IOtpSender`/`IOtpVerifier` are Application-layer interfaces; concrete implementations (sms.ir client, MinIO/S3 client, ASP.NET Identity token provider adapter) live in Infrastructure.

## 4. Module boundaries inside the monolith

Even though this is one solution/deployable, keep folder/namespace boundaries between modules so a future extraction is possible without a rewrite:

```
Modules (by bounded context, not by technical layer):
  - Obligations      (Obligation aggregate, RecurrenceRule, Reminder, Attachment, ActivityLog)
  - Users            (User, Auth/OTP)
  - Taxonomy         (Category, Tag)
  - Relationships     (Person, Asset) — should-have, added post-MVP-first-slice
  - Notifications      (NotificationLog, ISmsSender/IEmailSender adapters, background sender job)
```

Each module has its own Domain/Application slice; Infrastructure and API can be organized by module too, or shared where thin (e.g. a shared `DbContext` is acceptable in a monolith — module separation is about code/namespace boundaries, not necessarily separate databases).

## 5. Auth flow (high level)

```
1. Client POST /api/v1/auth/otp/request { phoneNumber }
   → Application generates an OTP via ASP.NET Core Identity's
     PhoneNumberTokenProvider, Infrastructure's ISmsSender delivers it.
2. Client POST /api/v1/auth/otp/verify { phoneNumber, code }
   → Application verifies the token via Identity's UserManager.
     On success: user created if new (PhoneNumberConfirmed = true),
     JWT issued.
3. Client uses the JWT as Bearer token on every subsequent request.
   Every Application-layer query/command is scoped by UserId
   extracted from the token (via ICurrentUser).
```

No password exists anywhere in this flow. See `docs/SECURITY.md` for OTP rate-limiting and token handling details.

## 6. Recurrence & Reminder engines

- **Recurrence engine** lives in the Domain layer (pure calculation: given a `RecurrenceRule` and a reference date, compute the next occurrence date). No I/O, fully unit-testable.
- **Occurrence materialization** (writing the next `ObligationOccurrence` row) is an Application-layer use case, triggered either by a domain event (`ObligationCompleted`) or by the nightly background job (Infrastructure `IHostedService`) for auto-overdue detection.
- **Reminder engine** (deciding what's due to send) is an Application-layer query run by a background job every 5–15 minutes; sending itself goes through `ISmsSender`/`IEmailSender`; idempotency is enforced via `NotificationLog` at the Application layer, not left to the sender.

## 7. Frontend architecture

Feature-based folder structure (not type-based):

```
/src
  /features/obligations
  /features/dashboard
  /features/auth
  /features/categories
  /shared (ui components, hooks, date-utils, api-client)
```

State: React Query for server state, lightweight Context/Zustand for UI state (no full Redux in MVP). Forms: `react-hook-form` + Zod validation. A shared Jalali date-picker component lives in `/shared`.

## 8. Deployment shape

Single Docker Compose stack on a domestic (Iranian) VPS (ADR-0009 in `docs/DECISIONS.md`): `api` (ASP.NET Core), `postgres`, `minio` (file storage), `web` (built React app served via Nginx), and `reverse-proxy` (Caddy, automatic Let's Encrypt TLS — the only service exposed on ports 80/443). See root `docker-compose.yml` and `docs/SECURITY.md` for TLS and backup/recovery details. Every external dependency (SMS, storage) still stays provider-agnostic (see `AGENTS.md` §2) even though the VPS target is now fixed — a domestic provider can also change, and the interfaces exist precisely so that isn't a rewrite.
