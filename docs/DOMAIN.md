# DOMAIN.md — Personal Obligation Manager

> Source of truth for the domain model. Any schema/entity/enum change made in code MUST be reflected here in the same task (see `AGENTS.md` §7, Definition of Done). This file is the detailed counterpart to the summary in `AGENTS.md` §4.

---

## 1. Core Design Decision: One Central Entity, Not Many

`Obligation` is the single Aggregate Root for almost everything the user tracks (bill renewal, installment payment, contract end, license renewal, etc.). These "types" share the same behavioral shape — title, due date/range, status, optional recurrence, reminders, attachments, category — and differ only in a handful of type-specific fields.

Instead of a separate entity per obligation type (which would mean 10+ tables, 10+ CRUD surfaces, 10+ forms — overengineering for a solo developer + AI agent), the type-specific data lives in an `ExtraFields` JSONB column on `Obligation`. Adding a new obligation type later means adding validation in the Application layer, not a migration.

`Person` and `Asset` remain separate entities because they are **relationships**, not obligation types — an `Obligation` can optionally link to one of each.

---

The type-specific validation ownership is clarified in §4.1: `ExtraFields` rules are Domain invariants
implemented by a Domain validator; the Application layer only orchestrates the use case and maps errors.
This clarification supersedes the earlier summary sentence that described the validator as Application-layer logic.

## 2. Entities

### 2.1 User (MVP)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| PhoneNumber | string | Unique. Used as the Identity username. E.164 or normalized Iranian format. |
| PhoneNumberConfirmed | bool | Set true after first successful OTP verification. |
| DisplayName | string | Optional. |
| CalendarPreference | enum: `Jalali` \| `Gregorian` | Default `Jalali`. Display/input preference only — storage is always UTC/Gregorian. |
| CreatedAt | datetime (UTC) | |
| DeletedAt | datetime? (UTC) | Soft delete / account deletion. |
| SecurityStamp | string | Identity token entropy (ADR-0016). Infrastructure-managed, not a domain concept — needed by the OTP token provider. |

Auth notes: no `PasswordHash` field exists anywhere. Backed by ASP.NET Core Identity with the **built-in** `PhoneNumberTokenProvider` over a custom `IUserStore` on the clean `users` table (no `AspNetUsers` schema). The OTP token validity window is framework-managed (~3–6 minutes via Identity's internal TOTP engine) and **not** precisely configurable — see ADR-0001 / ADR-0016. The `Id` is app-assigned (`Guid.NewGuid()` in `User.Create`).

### 2.2 Obligation (MVP — Aggregate Root)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK → User. Every query must filter by this. |
| Type | enum | `Task`, `Payment`, `Document`, `Subscription`, `Contract`, `Debt`, `Maintenance`, `Appointment`, `Custom` |
| Title | string | Required |
| Notes | text | Free text |
| StartDate | datetime? (UTC) | |
| DueDate | datetime? (UTC) | Optional; list ordering places null values last |
| EndDate | datetime? (UTC) | For obligations with a range (e.g. lease) |
| Status | enum | `Pending`, `Completed`, `Skipped`, `Overdue`, `Archived` |
| Priority | enum | `Low`, `Medium`, `High` |
| CategoryId | Guid? | Nullable during task 0006; task 0007 adds category persistence and ownership validation (ADR-0019) |
| PersonId | Guid? | Nullable FK → Person |
| AssetId | Guid? | Nullable FK → Asset |
| ExtraFields | JSONB | Type-specific data (see §4), always a JSON object and defaults to `{}` |
| IsRecurring | bool | |
| CreatedAt / UpdatedAt / DeletedAt | datetime (UTC) | Soft delete supported |

Dates are always stored UTC/Gregorian. Convert to Jalali only in the presentation layer — this is critical so recurrence/reminder calculations never hit calendar bugs.

**Lifecycle and restore semantics (MVP):**

- `Completed`, `Skipped`, and `Archived` are terminal/locked states for detail updates; the API reports update attempts as `409 Conflict`.
- `SoftDelete()` sets `DeletedAt` and is owner-scoped by every repository query.
- `Restore()` clears `DeletedAt` and may restore any soft-deleted obligation that has not been hard-deleted. No retention window is enforced in MVP.
- `Restore()` raises `ObligationRestored`, alongside the existing lifecycle domain events. A future hard-delete/retention policy may narrow restore eligibility and requires a new decision.
- When `DueDate` is null, no due-date range comparison is performed; if a due date is present, it must remain within any supplied start/end range.

### 2.3 RecurrenceRule (MVP)

1:0..1 with `Obligation`, present only when `IsRecurring = true`.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ObligationId | Guid | FK, unique |
| Frequency | enum | `Daily`, `Weekly`, `Monthly`, `Yearly`, `CustomInterval` |
| Interval | int | Every N days/weeks/months/years |
| DayOfMonthRule | enum/value object | `FixedDay(n)` \| `LastDayOfMonth` \| `NthWeekday(n, weekday)` (should-have, lower priority) |
| CalendarSystem | enum | `Jalali` \| `Gregorian` — which calendar the pattern is computed against |
| StartDate | datetime (UTC) | |
| EndDate | datetime? (UTC) | Null = infinite |
| OccurrenceCount | int? | Alternative to EndDate |
| OnMissed | enum | `CarryOverAsOverdue` (default), `AutoSkipAndCreateNext`, `RequireManualAction` |

MVP patterns: daily, weekly, monthly (fixed day), monthly (last day), yearly (Jalali or Gregorian anniversary), "every N days/weeks/months/years". `NthWeekday` is should-have.

**Occurrence generation strategy:** lazy — only the current/next occurrence is materialized, not all future ones. A nightly background job creates the next occurrence once the current one is completed or has passed due date. This prevents unbounded database growth.

**Default missed behavior:** `CarryOverAsOverdue` — a missed recurring occurrence stays open in `Overdue` status until the user completes/skips it; the next occurrence is only created after that action (avoids piling up meaningless reminders).

### 2.4 ObligationOccurrence (MVP)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ObligationId | Guid | FK |
| DueDate | datetime (UTC) | |
| Status | enum | Same set as Obligation.Status |
| CompletedAt | datetime? (UTC) | |

### 2.5 Reminder (MVP)

0..* per `Obligation`.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ObligationId | Guid | FK |
| OffsetType | enum | `BeforeDue`, `OnDue`, `AfterDue` |
| OffsetDays | int | e.g. 30, 7, 3, 1, 0, -3 |
| Channel | enum | `Sms` (primary/default), `Email` (low priority), `BrowserPush` (should-have) |

Suggested defaults on obligation creation (user can add/remove): 7 days before, 1 day before, on due date.

**Overdue behavior:** if the due date passes and the obligation is still `Pending`, an automatic system-generated reminder (not user-defined) is sent weekly, up to 3 times, until the user completes/skips/archives it — to avoid spam.

### 2.6 NotificationLog (MVP)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ReminderId | Guid | FK |
| ObligationId | Guid | FK |
| SentAt | datetime (UTC) | |
| Channel | enum | Same set as Reminder.Channel |
| Status | enum | `Sent`, `Failed` |

One row per (ObligationId, ReminderId, send date) — used for idempotency so a re-run background job never double-sends.

Notification system stays intentionally simple: no `Notification`/`Channel`/`Template`/`Schedule` entities. `Reminder` defines "when," `NotificationLog` records "what happened." Message templates are hard-coded in application code, not stored in the database. Channel is an enum, not an entity. Adding a new channel later = implementing a new `ISmsSender`-style interface, no schema change.

### 2.7 Category (MVP)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid? | Null = system default category |
| Name | string | |
| IsDefault | bool | |
| Icon | string | |

Category names are trimmed and must be 1–100 characters; icons are optional and limited to 100
characters. System defaults have `UserId = null` and are immutable through the user-facing API.
User-owned categories are scoped by `UserId`.

### 2.8 Tag / ObligationTag (MVP, should-have priority)

| Field | Type | Notes |
|---|---|---|
| Tag.Id | Guid | PK |
| Tag.UserId | Guid | FK |
| Tag.Name | string | |
| ObligationTag.ObligationId | Guid | FK (composite PK with TagId) |
| ObligationTag.TagId | Guid | FK |

### 2.9 Attachment (MVP)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ObligationId | Guid | FK |
| FileName | string | |
| StorageKey | string | Key/path in `IFileStorage` |
| MimeType | string | Allowed: PDF, JPG, PNG, HEIC (auto-converted server-side to JPEG if needed) |
| SizeBytes | long | Max 10 MB per file, max 5 files per obligation (quota-adjustable later) |
| UploadedAt | datetime (UTC) | |
| DeletedAt | datetime? (UTC) | Soft delete, 30-day recovery window |

Files are private; access only via short-lived signed URLs or authenticated API calls. No versioning in MVP — a new upload replaces the old one, or the user uploads a separate new attachment.

### 2.10 ActivityLog (MVP, should-have priority)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ObligationId | Guid | FK |
| UserId | Guid | FK |
| Action | string | e.g. `Created`, `Completed`, `Postponed`, `Skipped` |
| Changes | JSONB | Diff of changed fields |
| CreatedAt | datetime (UTC) | |

### 2.11 Person (should-have, MVP+1)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK |
| Name | string | |
| Phone | string? | |
| Note | text? | |

Used as counterparty for debt/credit, cheques, lease landlord, etc.

### 2.12 Asset (should-have, MVP+1)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK |
| Name | string | |
| Type | enum | `Vehicle`, `Device`, `Property`, `Appliance`, `Other` |
| Metadata | JSONB | Optional, e.g. plate number, model |

A "single asset page listing all related obligations" (e.g. full car service history) is a strong differentiator but not required for the first usable release — tags/categories can substitute until this ships.

---

## 3. Relationships

```
User 1---* Obligation
Obligation 1---0..1 RecurrenceRule
Obligation 1---* ObligationOccurrence   (recurring only)
Obligation 1---* Reminder
Obligation 1---* Attachment
Obligation *---* Tag
Obligation *---1 Category
Obligation *---0..1 Person   (nullable FK)
Obligation *---0..1 Asset    (nullable FK)
```

---

## 4. ExtraFields by Obligation Type (financial types)

No accounting layer — just the minimal fields needed on `ExtraFields` per type:

| Type | Minimal fields |
|---|---|
| Installment/Loan | Amount, TotalInstallments, CurrentInstallmentNo, DueDayOfMonth |
| Debt/Credit | Amount, CounterpartyPersonId, Direction (`Owe`/`Owed`), DueDate |
| Cheque | Amount, CounterpartyPersonId, ChequeNumber (optional text), DueDate |
| Rent | Amount, LandlordPersonId, DueDayOfMonth, LeaseEndDate (this itself is often also a separate "Contract end" Obligation) |
| Bill/Utility | Amount (nullable — variable), DueDate |
| Subscription | Amount, BillingCycle (`Monthly`/`Yearly`), NextChargeDate |

Money-like `Amount` values are signed 64-bit integer Rial values; never use floating-point or implicit decimal
rounding. No automatic aggregate calculations (e.g. "total debt") in MVP; a simple dashboard sum is should-have if desired.

Persisted amounts in the relational schema (outside ExtraFields where applicable) follow the fixed rule from `AGENTS.md`: `bigint` Rial, never `float`/`double`.

### 4.1 MVP domain validation contract

`ExtraFields` validation is a Domain invariant implemented by a per-type validator (for example,
`IExtraFieldsValidator`). The validator rejects malformed JSON, missing required fields, unknown enum
values, and money-like values that are not representable as a signed 64-bit integer. It performs no
accounting calculations, conversions, or aggregation.

For the current obligation types, the MVP schemas are:

| Type | Required/validated fields |
|---|---|
| `Payment` | `amount` (required signed 64-bit integer Rial); optional `currency` string |
| `Document` | `documentType` (required non-empty string); attachments hold file contents |
| `Subscription` | `amount` (required signed 64-bit integer Rial), `billingCycle` (`Monthly`/`Yearly`), `nextChargeDate` (UTC datetime) |
| `Task`, `Contract`, `Debt`, `Maintenance`, `Appointment`, `Custom` | No additional required fields in MVP; payload must still be a JSON object |

Adding a new type-specific schema requires a Domain change and tests, not a database migration.

---

## 5. Explicitly NOT Built in MVP

Do not create these as separate entities without an explicit task requesting it:

- `Organization` (separate from `Person`)
- `Event` as an independent entity (obligations are event-triggered via `ExtraFields`/notes, not a separate Event entity)
- `NotificationChannel` / `NotificationTemplate` / `NotificationSchedule` as entities — these are code config in MVP
- `Contract` as a separate entity — it's an `Obligation.Type = Contract`
- `Document` as a separate entity — `Attachment` covers this

---

## 6. Relational Schema Reference

```sql
users (id, phone_number, phone_number_confirmed, display_name, calendar_pref, created_at, deleted_at)

obligations (
  id, user_id, type,               -- enum: Task, Payment, Document, Subscription, Contract, Debt, Maintenance, Appointment, Custom
  title, notes,
  start_date, due_date, end_date,  -- all Gregorian/UTC
  status,                          -- Pending, Completed, Skipped, Overdue, Archived
  priority,                        -- Low, Medium, High
  category_id (nullable in task 0006; category FK arrives in task 0007), person_id (nullable), asset_id (nullable),
  extra_fields JSONB,              -- amount, currency, installment_no, ...
  is_recurring bool,
  created_at, updated_at, deleted_at
)

recurrence_rules (
  id, obligation_id, frequency, interval, day_of_month_rule,
  calendar_system, start_date, end_date, occurrence_count, on_missed
)

obligation_occurrences (
  id, obligation_id, due_date, status, completed_at
)

reminders (
  id, obligation_id, offset_type, offset_days, channel
)

notification_log (
  id, reminder_id, obligation_id, sent_at, channel, status
)

categories (id, user_id nullable, name, is_default, icon)
tags (id, user_id, name)
obligation_tags (obligation_id, tag_id)

attachments (id, obligation_id, file_name, storage_key, mime_type, size_bytes, uploaded_at, deleted_at)

people (id, user_id, name, phone, note)
assets (id, user_id, name, type, metadata JSONB)

activity_log (id, obligation_id, user_id, action, changes JSONB, created_at)
```

Design note: `extra_fields JSONB` means adding a new obligation type later (e.g. "anniversary") doesn't require a migration — only new validation logic in the Domain layer.

---

## 7. Changelog

| Date | Change | Related decision |
|---|---|---|
| Initial | Domain model created from spec §6–7, 14–19, 25 | — |
| Initial | `User` auth fields changed from Email/Password to PhoneNumber + OTP | See `AGENTS.md` §0 |
| 2026-08-23 | Added the core `Obligation` aggregate, status/priority/type enums, and nullable category reference pending taxonomy work | ADR-0019 |
