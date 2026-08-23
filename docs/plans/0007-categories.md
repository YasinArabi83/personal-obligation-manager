# 0007 — Category entity, CRUD, and system defaults

- Status: Done
- Date: 2026-08-23
- Related issue/task: `PROGRESS.md` Phase 1 `0007` — Category entity + CRUD endpoints + default system categories

## Goal

Introduce the `Category` supporting entity and its user-scoped application/API surface. Add the `categories` table and the deferred foreign key from `obligations.category_id`, expose authenticated list/create/update/delete endpoints, and provision a stable set of system-default categories that are visible to every authenticated user but cannot be modified or deleted by an individual user.

## Affected modules/layers

- Domain: Add the `Category` entity and its invariants (required name, optional icon, system-default versus user-owned category semantics). Keep it independent of EF Core and ASP.NET Core.
- Application: Add category DTO/use-case contracts, a repository port, current-user ownership rules, validation, and the default-category provisioning abstraction or service. Category reads must include system defaults plus the current user’s own categories; mutations must reject system defaults and other users’ categories.
- Infrastructure: Add `PomDbContext.Categories`, EF configuration/repository, the migration that creates `categories`, adds the `obligations.category_id` foreign key/index, and idempotent default-category seeding. Register all services through the existing composition root.
- API: Add an authenticated `/api/v1/categories` minimal-API group with dedicated request/response DTOs and the standard `{ error: { code, message } }` error shape. No entity is exposed directly.
- Frontend: Untouched; task 0009/0010 may consume the category API later.
- Tests: Add Domain unit tests, Infrastructure/PostgreSQL persistence tests, and API integration tests covering authentication, CRUD, defaults, and explicit cross-user isolation.
- Documentation: Update `docs/DOMAIN.md`, `docs/DATABASE.md`, `docs/API.md`, `docs/DECISIONS.md` (for any seeding/deletion policy resolved during implementation), and `docs/PROGRESS.md` after completion.

## Files to add/change

- `src/Domain/Taxonomy/Category.cs`
- `src/Application/Taxonomy/Ports/ICategoryRepository.cs`
- `src/Application/Taxonomy/CategoryAppService.cs` (or equivalent use-case service)
- `src/Infrastructure/Persistence/PomDbContext.cs`
- `src/Infrastructure/Persistence/Configurations/Taxonomy/CategoryConfiguration.cs`
- `src/Infrastructure/Persistence/Repositories/CategoryRepository.cs`
- `src/Infrastructure/Persistence/Seeding/DefaultCategorySeeder.cs` (or equivalent idempotent provisioning component)
- `src/Infrastructure/Persistence/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/Api/Endpoints/CategoryEndpoints.cs`
- `src/Infrastructure/Migrations/<timestamp>_AddCategoriesAndCategoryForeignKey.cs`
- `src/Infrastructure/Migrations/<timestamp>_AddCategoriesAndCategoryForeignKey.Designer.cs`
- `src/Infrastructure/Migrations/PomDbContextModelSnapshot.cs`
- `src/tests/POM.Domain.Tests/Taxonomy/CategoryTests.cs`
- `src/tests/POM.Infrastructure.Tests/CategoryRepositoryTests.cs`
- `src/tests/POM.Api.Tests/CategoryFlowTests.cs`
- `docs/DOMAIN.md`
- `docs/DATABASE.md`
- `docs/API.md`
- `docs/DECISIONS.md` (only when a meaningful implementation trade-off is made)
- `docs/PROGRESS.md`

Existing unrelated working-tree changes must be preserved.

## API contract

All routes require a valid JWT and are scoped to the current user:

```text
GET    /api/v1/categories
POST   /api/v1/categories
       Body: { "name": "string", "icon": "string?" }
PUT    /api/v1/categories/{id}
       Body: { "name": "string", "icon": "string?" }
DELETE /api/v1/categories/{id}
```

The list response contains system defaults (`userId = null`) and the current user’s categories, but never another user’s categories. Create returns `201 Created` with a category DTO; update/delete return `404 not_found` when the id is absent, belongs to another user, or is a system default, avoiding existence leaks. Invalid names return `400 validation_error`. Deleting a category that is referenced by obligations must follow the implementation policy selected before coding (prefer a safe conflict response or nulling the optional reference; do not silently orphan data).

## Schema changes

Add `categories`:

- `id uuid` app-assigned primary key
- `user_id uuid null` referencing `users(id)`; `NULL` means system default
- `name text not null`
- `is_default boolean not null default false`
- `icon text null`

Add the deferred nullable foreign key `obligations.category_id -> categories.id` and an index for category lookups. Keep `category_id` nullable so existing obligations and uncategorized obligations remain valid. The migration must be reversible and must not alter unrelated tables. Seed system defaults idempotently (safe on repeated startup/test migration) using stable identifiers or a unique natural-key strategy; user-created categories must never collide with or mutate those rows.

## Implementation outline

1. Confirm the category aggregate/entity boundary and validation rules against `docs/DOMAIN.md`; decide the maximum/normalization rules for `Name` and the behavior when a category is referenced by an obligation.
2. Add a pure Domain `Category` model with app-assigned `Guid` identity, nullable `UserId`, `IsDefault`, required name, optional icon, and methods that prevent invalid state transitions (especially mutation of system defaults).
3. Define an Application repository/use-case surface that makes `userId` mandatory for every user-facing operation. Implement list semantics as `(UserId IS NULL) OR (UserId = currentUserId)`.
4. Add API DTOs and endpoints under `/api/v1/categories`, require authorization, validate request bodies, map not-found/conflict/validation outcomes to the standard error response, and never accept a user id from the client.
5. Add EF mapping with snake_case conventions, app-assigned keys, user FK, indexes, and the nullable obligation category FK. Generate the single task migration and verify its `Up()`/`Down()` against PostgreSQL 18.
6. Implement idempotent system-default provisioning in Infrastructure. Keep the default set small and stable, document the chosen labels/identifiers, and ensure provisioning does not overwrite user edits or create duplicates.
7. Register repositories/services and map the endpoint group from the API composition root without introducing an API → Infrastructure dependency outside DI wiring.
8. Add xUnit tests:
   - Domain: valid creation, required-name validation, and system-default immutability.
   - Infrastructure: migration/persistence round-trip, default seeding idempotency, list visibility, and category-FK behavior.
   - API: authenticated CRUD, system defaults included in list, user A cannot read/update/delete user B’s category, anonymous requests are rejected, and referenced-category deletion follows the selected policy.
9. Update `DOMAIN.md`, `DATABASE.md`, and `API.md`; log any deletion/seeding or FK behavior decision immediately in `DECISIONS.md`; then mark this plan Done and update `PROGRESS.md`.

## Risks / open questions

- The exact system-default category labels are not specified in the current source-of-truth docs. Before implementation, select a small stable set and record the names/identifiers in `DECISIONS.md` so seeding is deterministic.
- Deleting a category referenced by an obligation needs an explicit policy. Because `obligations.category_id` is optional, either null the reference intentionally or return a documented conflict; never cascade-delete obligations.
- A system default is globally visible (`UserId = null`) but must be protected from user mutation/deletion. Enforce this in the Application layer and, where practical, in repository predicates.
- Category names may need case/whitespace normalization and a uniqueness rule. Prefer uniqueness per owner (and separately for system defaults) without preventing different users from choosing the same name; document the exact collation/normalization choice if introduced.
- Seeding must be safe when multiple application instances/startups race. The implementation should rely on stable ids or a database uniqueness constraint plus conflict-safe insertion, not an unguarded “check then insert.”
- This task adds category persistence and CRUD only; do not implement tag relationships, frontend screens, or full Obligation CRUD scheduled for later tasks.

## Acceptance criteria

- [ ] `Category` is a pure Domain entity with required-name validation and system-default immutability.
- [ ] `categories` migration is reversible, adds the nullable `obligations.category_id` FK and index, and changes no unrelated schema.
- [ ] System-default categories are seeded idempotently and visible to every authenticated user.
- [ ] Authenticated CRUD endpoints exist at `/api/v1/categories`; DTOs are separate from the entity and responses use the documented error shape.
- [ ] Every query and mutation is scoped by `UserId`; User A cannot read or modify User B’s categories, and system defaults cannot be modified/deleted.
- [ ] Referenced-category deletion follows an explicit, tested, documented policy and never deletes obligations.
- [ ] At least one xUnit unit test and one integration test cover the feature, including data isolation.
- [ ] `docs/DOMAIN.md`, `docs/DATABASE.md`, `docs/API.md`, and `docs/DECISIONS.md` (when applicable) match the implementation.
- [ ] Relevant backend tests pass with no new warnings.
- [ ] `docs/PROGRESS.md` is updated and this plan is marked `Status: Done` after implementation.
