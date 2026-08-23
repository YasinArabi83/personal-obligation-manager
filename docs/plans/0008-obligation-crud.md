# 0008 — Full Obligation CRUD API and lifecycle actions

- Status: Done
- Date: 2026-08-23
- Related issue/task: `PROGRESS.md` Phase 1 `0008` — Full Obligation CRUD API (complete/postpone/skip/archive) per `docs/API.md` §3

## Goal

Expose the authenticated Obligation API described in `docs/API.md` §3: paginated list/filter/search, create, read, update, soft delete, restore, and lifecycle actions (complete, postpone, skip, archive). Keep all behavior behind Application use cases and the existing DDD `Obligation` aggregate, with DTOs at the API boundary and strict per-user isolation.

## Affected modules/layers

- Domain: Add explicit `Obligation.Restore()` behavior and the `ObligationRestored` domain event. Add a provider-independent per-type `ExtraFields` validator as a Domain invariant; keep all domain code free of EF Core/ASP.NET dependencies.
- Application: Add an obligation application service/use-case contracts and DTOs for list/detail/create/update/action operations. Orchestrate Domain validation, category ownership checks, pagination/filter normalization, and error mapping; do not define business `ExtraFields` schemas in the API layer.
- Infrastructure: Expand `IObligationRepository` implementation with owner-scoped list/filter/search queries and an explicitly named soft-deleted restore lookup. Apply a global EF Core `DeletedAt IS NULL` query filter for normal Obligation queries; use only the owner-scoped bypass for restore. Use parameterized `ILIKE` search and no pg_trgm/full-text migration in MVP.
- API: Add an authorized `/api/v1/obligations` endpoint group with dedicated request/response DTOs and the standard error envelope. Map not-found, validation, conflict, and invalid-transition outcomes without exposing whether another user's obligation exists.
- Frontend: Untouched; task `0009` consumes the list surface and task `0010` consumes create/edit contracts.
- Tests: Add xUnit unit tests for application validation and lifecycle orchestration, PostgreSQL-backed repository tests for filtering/pagination/soft-delete behavior, and API integration tests for the complete flow and explicit User A/User B isolation.
- Documentation: Update `docs/API.md` with concrete request/response/pagination/error details, update `docs/DOMAIN.md` only if aggregate restore semantics or invariants change, update `docs/DATABASE.md` if a migration/index is added, log meaningful decisions in `docs/DECISIONS.md`, and update `docs/PROGRESS.md` when complete.

## Files to add/change

- `src/Application/Obligations/ObligationAppService.cs` (or equivalent use-case service)
- `src/Application/Obligations/ObligationDtos.cs` (or colocated dedicated request/response records)
- `src/Application/Obligations/Ports/IObligationRepository.cs`
- `src/Application/Taxonomy/Ports/ICategoryRepository.cs` only if an ownership-validation query is missing
- `src/Domain/Obligations/Obligation.cs`
- `src/Domain/Obligations/ExtraFields/` (per-type validator and validation errors)
- `src/Infrastructure/Persistence/Repositories/ObligationRepository.cs`
- `src/Api/Endpoints/ObligationEndpoints.cs`
- `src/Infrastructure/Persistence/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/tests/POM.Domain.Tests/Obligations/ObligationTests.cs`
- `src/tests/POM.Domain.Tests/Obligations/ExtraFieldsValidatorTests.cs`
- `src/tests/POM.Infrastructure.Tests/ObligationRepositoryTests.cs`
- `src/tests/POM.Api.Tests/ObligationFlowTests.cs`
- `src/tests/POM.Application.Tests/ObligationAppServiceTests.cs` (add the project if Application tests do not yet exist)
- `docs/API.md`
- `docs/DOMAIN.md` (only when domain semantics change)
- `docs/DATABASE.md` (only when a schema/index change is made)
- `docs/DECISIONS.md` (for resolved restore retention/search/validation trade-offs)
- `docs/PROGRESS.md`
- `src/Infrastructure/Migrations/<timestamp>_AllowNullableObligationDueDate.cs`
- `src/Infrastructure/Migrations/<timestamp>_AllowNullableObligationDueDate.Designer.cs`
- `src/Infrastructure/Migrations/PomDbContextModelSnapshot.cs`

## API contract

Implement the routes already listed in `docs/API.md`:

```text
GET    /api/v1/obligations?status=&type=&category=&from=&to=&q=&page=&pageSize=
POST   /api/v1/obligations
GET    /api/v1/obligations/{id}
PUT    /api/v1/obligations/{id}
DELETE /api/v1/obligations/{id}
POST   /api/v1/obligations/{id}/complete
POST   /api/v1/obligations/{id}/postpone
POST   /api/v1/obligations/{id}/skip
POST   /api/v1/obligations/{id}/archive
POST   /api/v1/obligations/{id}/restore
```

Request and response records must be separate from `Obligation`. Create/update payloads include the aggregate's core fields (`type`, `title`, `notes`, `startDate`, `dueDate`, `endDate`, `priority`, `categoryId`, `extraFields`, and `isRecurring` where supported by the current model). Postpone accepts `{ "newDueDate": "datetime" }`; skip accepts no request body because skip reasons are out of scope for MVP.

Normal list/get/update/delete operations exclude soft-deleted obligations through the global EF query filter. Restore is the only operation that uses the explicit owner-scoped filter bypass; no retention window is enforced in MVP. All dates are ISO-8601 UTC in transport; money-like values inside `ExtraFields` remain signed 64-bit integer Rial values.

List ordering is deterministic: `DueDate ASC, Id ASC`, with null due dates last. The `q` filter uses parameterized `ILIKE` in MVP.

Updating an obligation whose status is `Completed`, `Skipped`, or `Archived` returns HTTP `409 Conflict` with `{ "error": { "code": "conflict", "message": "A closed obligation cannot be edited." } }`.

## Schema changes

Add one reversible migration to make `obligations.due_date` nullable, matching the domain/API contract and
allowing deterministic `NULLS LAST` ordering. The existing category FK, ownership indexes, and `deleted_at`
column remain unchanged. No search extension/index migration is included.

The resolved MVP search strategy is parameterized `ILIKE`; a later pg_trgm/full-text migration requires a separate
ADR. Do not add recurrence, reminder, attachment, tag, person, or asset tables in this task.

## Implementation outline

1. Compare the existing aggregate methods and repository contract with the API contract; identify the smallest missing capability (especially restore and a way to query soft-deleted rows).
2. Define filter and pagination primitives with bounded defaults/maxima for `page` and `pageSize`; normalize optional status/type/category/date/text filters without accepting a client `userId`.
3. Implement the Application service:
   - create and update through `Obligation.Create`/`UpdateDetails`;
   - validate category ownership/default visibility;
   - invoke the Domain-owned per-type `ExtraFields` validator and map deterministic validation failures;
   - orchestrate complete/postpone/skip/archive/restore and map terminal-state exceptions to `409 Conflict`;
   - map aggregates to DTOs only.
4. Expand the Infrastructure repository with owner-scoped list/count or page queries, parameterized `ILIKE` search over title/notes, date/status/type/category filters, and deterministic `DueDate ASC, Id ASC` ordering with explicit `NULLS LAST` semantics. Add a clearly named owner-scoped restore lookup that bypasses the global query filter only for that operation.
5. Add the authorized minimal-API endpoint group and consistent error mapping (`validation_error`, `not_found`, `conflict`, `unauthorized`); ensure anonymous requests are rejected by the existing JWT pipeline.
6. Apply the resolved hardening decisions from ADR-0021 through ADR-0026: no restore retention window, no skip reason, Domain-owned `ExtraFields` validation, parameterized `ILIKE`, global soft-delete filtering with an explicit restore bypass, terminal-update `409 Conflict`, and nullable `DueDate` with explicit `NULLS LAST` ordering.
7. Add tests:
   - Domain: create/update validation, nullable-date range rules, per-type `ExtraFields` valid/missing/wrong-type cases, legal/illegal transitions, restore event, and no-retention behavior.
   - Infrastructure: persistence round-trip, global filter behavior, explicit restore bypass, all filters, ILIKE search, `NULLS LAST` ordering, page boundaries, and owner scoping against PostgreSQL 18.
   - API: authenticated create/list/get/update/delete/action/restore flow, DTO shape/status codes, invalid payloads, one `409` update test per terminal state, anonymous rejection, and User A cannot read/update/restore User B's obligation (assert `404`).
8. Run the complete backend test suite, update the API/domain/database/decision/progress docs as applicable, mark this plan `Status: Done`, and leave unrelated working-tree changes untouched.

## Resolved decisions / implementation risks

- Restore has no MVP retention window. `Obligation.Restore()` clears `DeletedAt`, raises `ObligationRestored`, and is reachable only through an owner-scoped repository lookup.
- `skip.reason` is removed from the API contract; no field or `ExtraFields` convention is added in MVP.
- `ExtraFields` validation is a Domain invariant. MVP schemas cover `Payment`, `Document`, and `Subscription`; money-like values are signed 64-bit integer Rial values, and validation performs no accounting.
- Persian text search starts with parameterized `ILIKE`. Revisit only on measured scale/latency or user-quality evidence; pg_trgm/full-text requires a new ADR and migration/deployment review.
- Updates to `Completed`, `Skipped`, or `Archived` obligations return `409 Conflict`; each terminal state requires an API test.
- List ordering is `DueDate ASC, Id ASC` with explicit `NULLS LAST` behavior; pagination tests must include null due dates. This requires the planned nullable-`DueDate` migration.
- A global EF Core query filter excludes soft-deleted obligations by default. Only a narrowly named, owner-scoped restore lookup may bypass it; isolation tests must cover both lookup and restore attempts.
- The hardening is tracked as a prerequisite within task `0008` because taxonomy task `0007` is already landed and the repository's numbering sequence cannot be safely renumbered.

## Acceptance criteria

- [ ] All ten obligation routes in `docs/API.md` §3 are implemented under an authorized `/api/v1/obligations` group.
- [ ] Every request/response uses dedicated DTOs; no domain aggregate is bound from or returned directly by the API.
- [ ] Create/update validate required fields, UTC date ordering, enum values, and Domain-owned per-type `ExtraFields` rules.
- [ ] Category references are checked for system-default/current-user ownership; cross-user category ids are rejected without existence leaks.
- [ ] List supports the documented filters, parameterized `ILIKE` search, bounded pagination, `DueDate ASC, Id ASC` ordering with `NULLS LAST`, and excludes soft-deleted rows through the global filter.
- [ ] Complete/postpone/skip/archive/restore invoke aggregate behavior and return stable success/error shapes; terminal updates return `409 Conflict`.
- [ ] Restore clears `DeletedAt`, raises `ObligationRestored`, has no MVP retention window, and uses an explicit owner-scoped filter bypass.
- [ ] User A cannot read, update, delete, restore, or transition User B's obligation; tests assert `404` for those attempts.
- [ ] At least one xUnit unit test and one PostgreSQL/API integration test cover the feature, including data isolation.
- [ ] The nullable-`DueDate` schema change has a reversible migration and matching `DATABASE.md` documentation.
- [ ] `docs/API.md`, applicable domain/database docs, `docs/DECISIONS.md` (ADR-0021 through ADR-0026), and `docs/PROGRESS.md` are updated, and this plan is marked `Status: Done` after implementation.
- [ ] Full relevant test suite passes with no new warnings and no leftover debug/commented-out code.
