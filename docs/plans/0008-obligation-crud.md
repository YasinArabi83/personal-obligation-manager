# 0008 — Full Obligation CRUD API and lifecycle actions

- Status: Draft
- Date: 2026-08-23
- Related issue/task: `PROGRESS.md` Phase 1 `0008` — Full Obligation CRUD API (complete/postpone/skip/archive) per `docs/API.md` §3

## Goal

Expose the authenticated Obligation API described in `docs/API.md` §3: paginated list/filter/search, create, read, update, soft delete, restore, and lifecycle actions (complete, postpone, skip, archive). Keep all behavior behind Application use cases and the existing DDD `Obligation` aggregate, with DTOs at the API boundary and strict per-user isolation.

## Affected modules/layers

- Domain: Reuse the existing `Obligation` aggregate transitions and add only behavior required by this task (notably safe restore semantics and any missing lifecycle invariant). Keep domain code free of EF Core/ASP.NET dependencies.
- Application: Add an obligation application service/use-case contracts and DTOs for list/detail/create/update/action operations. Validate request shape, enum values, UTC date ranges, JSON-object `ExtraFields`, and type-specific `ExtraFields` rules from `docs/DOMAIN.md` §4. Validate that a supplied category belongs to the current user (or is a system default) through the category port. Ensure every operation receives the authenticated `UserId`.
- Infrastructure: Expand `IObligationRepository` implementation with owner-scoped list/filter/search queries, soft-deleted lookup for restore, and persistence operations needed by the application service. Keep queries parameterized and exclude deleted rows from normal reads. Add only a schema/index migration if the chosen search implementation requires one; otherwise no database change.
- API: Add an authorized `/api/v1/obligations` endpoint group with dedicated request/response DTOs and the standard error envelope. Map not-found, validation, conflict, and invalid-transition outcomes without exposing whether another user's obligation exists.
- Frontend: Untouched; task `0009` consumes the list surface and task `0010` consumes create/edit contracts.
- Tests: Add xUnit unit tests for application validation and lifecycle orchestration, PostgreSQL-backed repository tests for filtering/pagination/soft-delete behavior, and API integration tests for the complete flow and explicit User A/User B isolation.
- Documentation: Update `docs/API.md` with concrete request/response/pagination/error details, update `docs/DOMAIN.md` only if aggregate restore semantics or invariants change, update `docs/DATABASE.md` if a migration/index is added, log meaningful decisions in `docs/DECISIONS.md`, and update `docs/PROGRESS.md` when complete.

## Files to add/change

- `src/Application/Obligations/ObligationAppService.cs` (or equivalent use-case service)
- `src/Application/Obligations/ObligationDtos.cs` (or colocated dedicated request/response records)
- `src/Application/Obligations/Ports/IObligationRepository.cs`
- `src/Application/Taxonomy/Ports/ICategoryRepository.cs` only if an ownership-validation query is missing
- `src/Domain/Obligations/Obligation.cs` only if restore/transition behavior is not sufficient for the documented API
- `src/Infrastructure/Persistence/Repositories/ObligationRepository.cs`
- `src/Api/Endpoints/ObligationEndpoints.cs`
- `src/Infrastructure/Persistence/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/tests/POM.Domain.Tests/Obligations/ObligationTests.cs` (additional lifecycle/restore cases if Domain changes)
- `src/tests/POM.Infrastructure.Tests/ObligationRepositoryTests.cs`
- `src/tests/POM.Api.Tests/ObligationFlowTests.cs`
- `src/tests/POM.Application.Tests/ObligationAppServiceTests.cs` (add the project if Application tests do not yet exist)
- `docs/API.md`
- `docs/DOMAIN.md` (only when domain semantics change)
- `docs/DATABASE.md` (only when a schema/index change is made)
- `docs/DECISIONS.md` (for resolved restore retention/search/validation trade-offs)
- `docs/PROGRESS.md`
- `src/Infrastructure/Migrations/<timestamp>_<description>.cs` and designer/snapshot (only if required by a deliberate schema/index decision)

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

Request and response records must be separate from `Obligation`. Create/update payloads include the aggregate's core fields (`type`, `title`, `notes`, `startDate`, `dueDate`, `endDate`, `priority`, `categoryId`, `extraFields`, and `isRecurring` where supported by the current model). Postpone accepts `{ "newDueDate": "datetime" }`; skip accepts `{ "reason": "string?" }` and must preserve the reason only if an existing domain/application field or documented representation supports it—do not invent a new persistence field in this task.

Normal list/get/update/delete operations exclude soft-deleted obligations. Restore is the only operation that loads a deleted row and must enforce the selected retention policy. All dates are ISO-8601 UTC in transport; money-like values inside `ExtraFields` remain integer Rial values.

## Schema changes

Expected: none. The `obligations` table, nullable category FK, ownership/query indexes, and `deleted_at` already exist from tasks `0006` and `0007`.

If PostgreSQL full-text/trigram search is selected instead of a parameterized `ILIKE` implementation, create one reversible migration for the required extension/index and document the operational impact. Do not add recurrence, reminder, attachment, tag, person, or asset tables in this task.

## Implementation outline

1. Compare the existing aggregate methods and repository contract with the API contract; identify the smallest missing capability (especially restore and a way to query soft-deleted rows).
2. Define filter and pagination primitives with bounded defaults/maxima for `page` and `pageSize`; normalize optional status/type/category/date/text filters without accepting a client `userId`.
3. Implement the Application service:
   - create and update through `Obligation.Create`/`UpdateDetails`;
   - validate category ownership/default visibility;
   - validate `ExtraFields` as a JSON object and apply per-type required-field checks in Application;
   - orchestrate complete/postpone/skip/archive/restore and map domain exceptions to stable error codes;
   - map aggregates to DTOs only.
4. Expand the Infrastructure repository with owner-scoped list/count or page queries, case/partial search over title/notes, date/status/type/category filters, and soft-delete-aware lookup methods. Ensure another user's id always behaves as not found.
5. Add the authorized minimal-API endpoint group and consistent error mapping (`validation_error`, `not_found`, `conflict`, `unauthorized`); ensure anonymous requests are rejected by the existing JWT pipeline.
6. Decide and record before implementation:
   - the restore retention duration and behavior when the window has expired;
   - whether `skip.reason` is represented in `ExtraFields` or intentionally ignored/rejected because no domain field exists;
   - the initial search strategy (`ILIKE` versus a schema/index migration).
7. Add tests:
   - Domain/Application: create/update validation, date ordering, JSON/type-specific validation, legal/illegal transitions, restore policy, category ownership rejection, and pagination normalization.
   - Infrastructure: persistence round-trip, all filters, search, stable ordering, page boundaries, deleted-row visibility, and owner scoping against PostgreSQL 18.
   - API: authenticated create/list/get/update/delete/action/restore flow, DTO shape/status codes, invalid payloads, closed-obligation conflicts, anonymous rejection, and User A cannot read/update/delete/restore User B's obligation (assert `404`).
8. Run the complete backend test suite, update the API/domain/database/decision/progress docs as applicable, mark this plan `Status: Done`, and leave unrelated working-tree changes untouched.

## Risks / open questions

- The current aggregate has `SoftDelete` but no explicit `Restore`; adding it must preserve DDD invariants and avoid reviving an obligation outside the documented retention window.
- `docs/API.md` lists `skip.reason`, but the current aggregate/schema has no dedicated reason field. Resolve this explicitly before coding; do not silently add a column or unrelated entity.
- `ExtraFields` type-specific validation needs a clear MVP rule set. Reject malformed/missing required fields deterministically, while avoiding accounting logic or floating-point money.
- Search over Persian text may be implemented with `ILIKE` first; adding `pg_trgm` or full-text infrastructure changes deployment/migration scope and requires an ADR.
- Updating a completed, skipped, or archived obligation is currently rejected by the aggregate. The API must expose this as a stable conflict/validation response and test it.
- List ordering must be deterministic (for example, due date then id) so pagination does not duplicate/omit rows between requests.
- Soft-deleted rows must not leak through counts or filters, while restore must remain owner-scoped and not reveal another user's deleted record.

## Acceptance criteria

- [ ] All ten obligation routes in `docs/API.md` §3 are implemented under an authorized `/api/v1/obligations` group.
- [ ] Every request/response uses dedicated DTOs; no domain aggregate is bound from or returned directly by the API.
- [ ] Create/update validate required fields, UTC date ordering, enum values, JSON-object `ExtraFields`, and applicable type-specific fields.
- [ ] Category references are checked for system-default/current-user ownership; cross-user category ids are rejected without existence leaks.
- [ ] List supports the documented filters, text search, bounded pagination, deterministic ordering, and excludes soft-deleted rows.
- [ ] Complete/postpone/skip/archive/restore invoke aggregate behavior and return stable success/error shapes; illegal transitions are covered.
- [ ] Soft delete and restore honor an explicitly documented retention policy.
- [ ] User A cannot read, update, delete, restore, or transition User B's obligation; tests assert `404` for those attempts.
- [ ] At least one xUnit unit test and one PostgreSQL/API integration test cover the feature, including data isolation.
- [ ] Any schema/index change has a reversible migration and matching `DATABASE.md` documentation; otherwise no migration is added.
- [ ] `docs/API.md`, applicable domain/database docs, `docs/DECISIONS.md`, and `docs/PROGRESS.md` are updated, and this plan is marked `Status: Done` after implementation.
- [ ] Full relevant test suite passes with no new warnings and no leftover debug/commented-out code.
