# TESTING.md — Personal Obligation Manager

> Testing strategy and conventions. Every new feature needs at least 1 unit test + 1 integration test (`AGENTS.md` §5/§7) — this file explains what that means in practice for this stack.

---

## 1. Frameworks

- Backend unit + integration tests: **xUnit** (ADR-0007 in `docs/DECISIONS.md`).
- Integration tests against a real Postgres: **Testcontainers** (spins up a disposable `postgres:18-alpine` container per test run/collection — no shared test database, no mocking the database in integration tests).
- Frontend: React Testing Library for component/form tests. Deeper E2E (Playwright) is should-have, not MVP.

## 2. Unit tests

Scope: Domain logic and Application services, with all Infrastructure interfaces mocked (`ISmsSender`, `IFileStorage`, repositories, etc.) — no real database, no real network calls.

Highest-priority coverage:
- **Recurrence engine** — this is the product's core, test it thoroughly:
  - Daily, weekly, monthly (fixed day), monthly (last day), yearly (Jalali anniversary), yearly (Gregorian anniversary), "every N days/weeks/months/years".
  - Edge cases: Esfand in a leap year, 31 Farvardin → next month has only 30 days, February 29 → Gregorian leap year handling, month-end rollovers.
  - `OnMissed` behavior: `CarryOverAsOverdue` (default), `AutoSkipAndCreateNext`, `RequireManualAction`.
- **Reminder engine idempotency** — running the due-reminder job twice for the same period must not send a duplicate notification (verified via `NotificationLog` checks in the use case, not just at the SMS provider level).
- **Money/Value Objects** — no float ever enters a calculation path; rounding behavior (if any conversion is ever needed) is explicit and tested.

## 3. Integration tests

Scope: API endpoints against a real Testcontainers Postgres instance.

Mandatory case for every list/get/update/delete endpoint touching user data:
- **Data isolation** — create Obligation as User A, attempt to read/update/delete it as User B, assert `404` (not `403` — avoid leaking existence, see `docs/SECURITY.md` §6).

Also cover:
- Auth flow: OTP request → verify → JWT issued → JWT required on protected endpoints → expired/invalid OTP rejected.
- Attachment upload/download: size limit (10MB), count limit (5 per obligation), signed URL access, and denial without auth.
- Recurrence/occurrence creation through the actual API surface (not just the domain unit test), to catch serialization/mapping bugs.

## 4. Coverage target

~70% on Domain + Application layers combined. This is a floor on the layers that carry business logic, not a blanket target across the whole codebase — a high percentage on generated DTOs or thin controllers is not the goal. Judge coverage quality on the recurrence/reminder engines specifically before trusting the aggregate number.

## 5. Frontend tests

Minimum for MVP: the Create Obligation form (validation, submit, error states) and the OTP login flow, tested with React Testing Library. Broader coverage and Playwright E2E are should-have, scheduled post-MVP unless a specific task calls for more.

## 6. Running tests

```bash
# Backend — all tests
dotnet test

# Backend — a single project
dotnet test src/tests/POM.Domain.Tests

# Frontend (Vitest + React Testing Library, in src/Web)
cd src/Web
npm test            # = vitest --run (same command used by CI)
```

(Adjust paths once the actual solution/workspace layout exists — keep this section accurate as the repo is scaffolded.)

## 7. CI expectations

Every push/PR runs the full backend test suite (unit + integration, via GitHub Actions with Testcontainers) and the frontend test suite. The workflow invokes the existing `npm test` script, which already expands to `vitest --run`. See `.github/workflows/ci.yml`. A PR with red tests is never merged; there is no "skip tests" escape hatch for this project.
