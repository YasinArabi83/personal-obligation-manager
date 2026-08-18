# PROGRESS.md — Task Status

> Updated at the end of every task (see `AGENTS.md` §6, step 12). Each item below maps 1:1 to a plan file `docs/plans/NNNN-slug.md` — the number here is the plan file number. Check items off as they're completed. Add new items if a task needs to be split further, but keep the numbering sequence intact (don't renumber existing entries).

Legend: `[ ]` not started · `[~]` in progress · `[x]` done · `[!]` blocked

---

## Phase 0 — Foundation

- [x] `0001` — Repo scaffolding: .NET 10 solution (Domain/Application/Infrastructure/Api) + React project in `src/Web`
- [x] `0002` — Database setup: EF Core + DbContext + first empty migration + Postgres wired in `docker-compose.yml`
- [x] `0003` — User entity + ASP.NET Core Identity configured with `PhoneNumberTokenProvider` (no password)
- [x] `0004` — OTP auth endpoints (`auth/otp/request`, `auth/otp/verify`) + JWT issuance + basic rate limiting
- [ ] `0005` — CI pipeline active on the real repo (backend + frontend jobs green)

## Phase 1 — Core Obligation

- [ ] `0006` — Obligation aggregate core (no recurrence/reminder yet) + repository + migration
- [ ] `0007` — Category entity + CRUD endpoints + default system categories
- [ ] `0008` — Full Obligation CRUD API (complete/postpone/skip/archive) per `docs/API.md` §3
- [ ] `0009` — Obligation list/filter/search page (frontend)
- [ ] `0010` — Create/edit Obligation form (frontend), including dynamic `ExtraFields` by type
- [ ] `0011` — Shared Jalali date component (frontend) + UTC↔Jalali conversion
- [ ] `0012` — Dashboard endpoint + UI (Today/Week/Month/Overdue) per `docs/API.md` §4

## Phase 2 — Recurrence + Reminder

- [ ] `0013` — RecurrenceRule entity + recurrence calculation engine in Domain (unit-tested per `docs/TESTING.md` §2)
- [ ] `0014` — Recurrence endpoint + UI for setting recurrence on an Obligation
- [ ] `0015` — Background job: lazy next-occurrence generation
- [ ] `0016` — Reminder entity + CRUD endpoints
- [ ] `0017` — `ISmsSender` real provider implementation (e.g. sms.ir)
- [ ] `0018` — Background job: due-reminder sending, idempotent via `NotificationLog`
- [ ] `0019` — Overdue handling: `OnMissed` logic + automatic weekly re-reminder (max 3x)

## Phase 3 — Attachments + Polish

- [ ] `0020` — `IFileStorage` implementation on MinIO + signed URLs
- [ ] `0021` — Attachment upload/download/delete API + UI (size/count limits)
- [ ] `0022` — Generic soft delete + restore within the recovery window
- [ ] `0023` — Activity log: automatic recording of meaningful changes per Obligation
- [ ] `0024` — Account endpoints (`account/me`, JSON export, account deletion)
- [ ] `0025` — Tag entity + many-to-many relation + UI

## Deployment

- [ ] `0026` — Real deploy to the Iranian VPS + Caddy/TLS setup per `docker-compose.yml` (ADR-0009)
- [ ] `0027` — Automated backups implemented + one real dry run of the recovery runbook (closes the open item in `docs/SECURITY.md` §5)

## Deferred to just past MVP (should-have)

- [ ] `0028` — Person entity
- [ ] `0029` — Asset entity + related-obligation history
- [ ] `0030` — Email notification channel
- [ ] `0031` — Full calendar view

## Explicitly out of scope (do not build without an explicit task)

- Accounting/bookkeeping features
- CRM features
- Native app
- AI-assisted features
- Multi-user / family sharing
