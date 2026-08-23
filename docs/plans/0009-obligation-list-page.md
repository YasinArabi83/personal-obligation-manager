# 0009 — Obligation list/filter/search page (frontend) + minimal OTP login

- Status: Done
- Date: 2026-08-24
- Related task: `PROGRESS.md` Phase 1 `0009` — Obligation list/filter/search page (frontend)

## Goal

Build the first real frontend surface: a Persian-language, RTL obligations list page that consumes
`GET /api/v1/obligations` from task `0008`, with status/type/category/date filters, debounced text
search (`q`), pagination synced to URL query params, and loading/empty/error states.

Because every obligation call requires a JWT and the app currently has no frontend auth at all
(and no dedicated auth-UI task exists in `PROGRESS.md`), this task also includes the **minimal**
phone + OTP login slice needed to make the page usable end-to-end: request OTP → verify → store
tokens → redirect to the list. It is deliberately thin; richer auth UX can be split out later if
needed.

## Scope decisions (user-approved for this plan)

- Minimal OTP login is included in this task (user decision).
- Routing uses **react-router** with filter/pagination state synced to URL query params (user
  decision); this also serves tasks `0010`+ and the dashboard (`0012`).
- Jalali date display is **out of scope** — task `0011` owns the shared Jalali component. This task
  renders dates as plain Gregorian (`YYYY-MM-DD`) behind one small formatting helper so `0011`
  replaces it in exactly one place.
- No backend changes: all endpoints consumed already exist (`docs/API.md` §2, §3, §8). No schema,
  migration, or API contract changes → `DOMAIN.md`/`DATABASE.md`/`API.md` untouched.

## Affected modules/layers

- Backend: untouched.
- Frontend (`src/Web`): first feature-based structure per `ARCHITECTURE.md §7` — `/src/features/auth`,
  `/src/features/obligations`, `/src/features/categories` (category filter source), `/src/shared`.
- Dependencies added to `src/Web/package.json`: `react-router-dom` (^7), `@tanstack/react-query` (^5).
  No form library yet (login inputs are plain controlled inputs; forms arrive with `0010` +
  react-hook-form/Zod per ARCHITECTURE.md §7).

## Files to add/change

```
src/Web/package.json                          (+ react-router-dom, @tanstack/react-query)
src/Web/src/main.tsx                          (router + QueryClientProvider)
src/Web/src/App.tsx                           (route tree: /login, / protected)
src/Web/src/index.css                         (RTL base styles if needed)
src/Web/src/shared/api/apiClient.ts           (fetch wrapper: base /api/v1, Bearer attach,
                                               single-flight refresh on 401, error envelope)
src/Web/src/shared/api/tokens.ts              (token storage + accessors)
src/Web/src/shared/api/error.ts               ({error:{code,message}} parsing)
src/Web/src/shared/types.ts                   (ObligationDto, enums as string unions, pagination)
src/Web/src/features/auth/LoginPage.tsx       (phone step → OTP step)
src/Web/src/features/auth/useAuth.ts          (context/hook: tokens, login, logout)
src/Web/src/features/auth/RequireAuth.tsx     (redirect guard)
src/Web/src/features/auth/auth.api.ts         (otp/request, otp/verify calls)
src/Web/src/features/obligations/ObligationsListPage.tsx
src/Web/src/features/obligations/ObligationsFilters.tsx
src/Web/src/features/obligations/ObligationRow.tsx (or card)
src/Web/src/features/obligations/Pagination.tsx
src/Web/src/features/obligations/useObligations.ts   (React Query hook keyed by filters)
src/Web/src/features/obligations/filtersUrl.ts       (URLSearchParams ↔ filter object)
src/Web/src/features/categories/useCategories.ts     (GET /api/v1/categories for the dropdown)
src/Web/src/App.test.tsx                      (update placeholder test)
src/Web/src/features/auth/LoginPage.test.tsx
src/Web/src/shared/api/apiClient.test.tsx
src/Web/src/features/obligations/ObligationsListPage.test.tsx
docs/DECISIONS.md                             (ADR: router/query/token-storage choices)
docs/PROGRESS.md                              (mark 0009 done after implementation)
```

## UI / data contract

- Filters map 1:1 to documented query params: `status` (Pending|Completed|Skipped|Overdue|
  Archived), `type` (Task|Payment|Document|Subscription|Contract|Debt|Maintenance|Appointment|
  Custom), `category` (id, options from `GET /api/v1/categories`), `from`/`to` (ISO-8601 UTC),
  `q`, `page`, `pageSize` (default 20, max 100). All optional; empty selection sends nothing.
- Enums arrive as strings (`JsonStringEnumConverter`) — typed as string unions.
- Response: `{ items, totalCount, page, pageSize, totalPages }`; ordering is server-side
  (`dueDate ASC, id ASC, NULLS LAST`) — the frontend never re-sorts.
- List row shows: title, type, status badge, priority, dueDate (Gregorian placeholder), category
  name (mapped from categories response). No lifecycle actions here (complete/skip/etc.) — those
  belong to detail/edit flows from task `0010`; this page is read-only.
- All UI text in Persian, `dir="rtl"`; Tailwind utility styling consistent with existing placeholder.

## Auth slice behavior

- `/login`: phone number input → `POST /auth/otp/request` (204 or error envelope; show
  `otp_rate_limited` message honoring `Retry-After`) → code input → `POST /auth/otp/verify` →
  store `{ accessToken, refreshToken, user }` → redirect to intended page.
- Tokens in `localStorage` (MVP trade-off, logged in DECISIONS.md; httpOnly-cookie pattern would
  need its own ADR).
- `apiClient` attaches `Authorization: Bearer`. On `401`, it runs a **single-flight** refresh via
  `POST /auth/refresh`, retries the original request once; on refresh failure it clears tokens and
  redirects to `/login`. Single-flight matters: refresh rotation revokes the whole family on replay
  (ADR-0017), so parallel requests must share one refresh call.
- `RequireAuth` guards `/` and redirects unauthenticated users to `/login`.

## Implementation outline

1. Add dependencies; set up router (`/login`, `/`), QueryClientProvider, RTL/Persian base.
2. Build `shared/api`: types, token storage, fetch wrapper with Bearer attach + single-flight
   refresh + error-envelope parsing.
3. Build auth feature: login page (two steps), `useAuth`, `RequireAuth`.
4. Build obligations feature: filters component, URL↔filter sync (`useSearchParams`, replace-mode),
   debounced `q` (~300 ms), React Query hook, rows, pagination, loading/empty/error states.
5. Tests (Vitest + RTL, mock `fetch`):
   - `apiClient.test` — bearer attach, 401→refresh→retry once, concurrent 401s trigger one refresh,
     envelope parsing.
   - `LoginPage.test` — happy path request→verify→redirect; 429 shows rate-limit message.
   - `ObligationsListPage.test` — renders mocked rows; changing a filter updates both URL params
     and the request query string; debounce collapses bursts into one call; pagination next/prev;
     empty and error states.
6. Run `npm run lint`, `npm test -- --run`, `npm run build`; manually verify against the local
   backend (Vite proxy → `localhost:8080`) including a real OTP round-trip.
7. Log ADR(s) in `DECISIONS.md` (react-router v7 + TanStack Query adoption; localStorage tokens),
   mark plan `Status: Done`, update `PROGRESS.md`.

## Risks / notes

- Refresh-family revocation (ADR-0017) makes naive refresh handling dangerous → single-flight
  queue is mandatory and tested; never fire `refresh` speculatively.
- OTP endpoints are rate-limited (3 req/10 min per phone; 5 failed verifies/hour) → login errors
  must surface the server envelope verbatim instead of generic retry prompts.
- Vite dev proxy already forwards `/api` to `localhost:8080` — no CORS work expected; keep all
  calls same-origin relative paths.
- Enum string values must stay in sync with the backend; they live once in `shared/types.ts`.

## Acceptance criteria

- [ ] `npm ci && npm run lint && npm test -- --run && npm run build` all pass in `src/Web`.
- [ ] End-to-end locally: OTP login → redirected to `/` → obligations list loads with real JWT.
- [ ] Every filter (`status`, `type`, `category`, `from`, `to`, `q`, `page`, `pageSize`) maps to the
      documented query param; URL reflects state; back/forward restores views.
- [ ] Pagination honors `totalCount`/`totalPages`; pageSize default 20.
- [ ] Loading, empty ("no results"), and error states render with the standard error message shape.
- [ ] Access-token expiry mid-session recovers via refresh without user action; failed refresh
      lands on `/login` with cleared tokens.
- [ ] The frontend never sends `userId` anywhere (server-side scoping per `API.md §1`).
- [ ] No Jalali rendering (deferred to `0011`); dates come from one shared formatting helper.
- [ ] No leftover `console.log` or commented-out code; backend tests unaffected and green.
