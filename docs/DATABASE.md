# DATABASE.md — Personal Obligation Manager

> Full relational schema and database conventions. The entity/field meanings live in `docs/DOMAIN.md`; this file focuses on the physical schema, migrations, and operational conventions.

---

## 1. Engine

PostgreSQL **18**, pinned in `docker-compose.yml` (`postgres:18-alpine`). Chosen for native JSONB support (used by `obligations.extra_fields`), no licensing cost, and easy self-hosting on any VPS regardless of provider (see `AGENTS.md` §2, provider-agnostic infrastructure rule).

## 2. Naming conventions

- Tables: `snake_case`, plural (`obligations`, `notification_log` is an exception kept singular for readability — avoid further exceptions).
- Columns: `snake_case`.
- Primary keys: `id`, type `uuid`.
- Foreign keys: `<referenced_table_singular>_id`, e.g. `obligation_id`, `user_id`.
- Enums: stored as PostgreSQL native `enum` types or as `text` with a CHECK constraint — pick one convention per table and stay consistent; EF Core enum-to-string conversion is acceptable to keep values human-readable in the DB.
- Timestamps: `timestamptz`, always UTC. Columns: `created_at`, `updated_at`, `deleted_at` (nullable — soft delete marker).

**Enforcement:** snake_case is applied globally by `EFCore.NamingConventions` in `PomDbContext.ConfigureOptions` (ADR-0014) — no manual `ToTable`/`HasColumnName` is needed for naming on new entities.

## 3. Full schema

```sql
users (
  id uuid primary key,
  phone_number text not null unique,
  phone_number_confirmed boolean not null default false,
  display_name text,
  calendar_pref text not null default 'Jalali',  -- 'Jalali' | 'Gregorian'
  created_at timestamptz not null default now(),
  deleted_at timestamptz
);

obligations (
  id uuid primary key,
  user_id uuid not null references users(id),
  type text not null,               -- Task, Payment, Document, Subscription, Contract, Debt, Maintenance, Appointment, Custom
  title text not null,
  notes text,
  start_date timestamptz,
  due_date timestamptz not null,
  end_date timestamptz,
  status text not null default 'Pending',   -- Pending, Completed, Skipped, Overdue, Archived
  priority text not null default 'Medium',  -- Low, Medium, High
  category_id uuid not null references categories(id),
  person_id uuid references people(id),
  asset_id uuid references assets(id),
  extra_fields jsonb not null default '{}',
  is_recurring boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  deleted_at timestamptz
);

recurrence_rules (
  id uuid primary key,
  obligation_id uuid not null unique references obligations(id),
  frequency text not null,          -- Daily, Weekly, Monthly, Yearly, CustomInterval
  interval int not null default 1,
  day_of_month_rule jsonb,          -- { "kind": "FixedDay", "day": 15 } | { "kind": "LastDayOfMonth" } | { "kind": "NthWeekday", "n": 1, "weekday": "Saturday" }
  calendar_system text not null,    -- Jalali, Gregorian
  start_date timestamptz not null,
  end_date timestamptz,
  occurrence_count int,
  on_missed text not null default 'CarryOverAsOverdue'
);

obligation_occurrences (
  id uuid primary key,
  obligation_id uuid not null references obligations(id),
  due_date timestamptz not null,
  status text not null,
  completed_at timestamptz
);

reminders (
  id uuid primary key,
  obligation_id uuid not null references obligations(id),
  offset_type text not null,        -- BeforeDue, OnDue, AfterDue
  offset_days int not null,
  channel text not null default 'Sms'   -- Sms, Email, BrowserPush
);

notification_log (
  id uuid primary key,
  reminder_id uuid not null references reminders(id),
  obligation_id uuid not null references obligations(id),
  sent_at timestamptz not null default now(),
  channel text not null,
  status text not null              -- Sent, Failed
);

categories (
  id uuid primary key,
  user_id uuid references users(id),   -- null = system default category
  name text not null,
  is_default boolean not null default false,
  icon text
);

tags (
  id uuid primary key,
  user_id uuid not null references users(id),
  name text not null
);

obligation_tags (
  obligation_id uuid not null references obligations(id),
  tag_id uuid not null references tags(id),
  primary key (obligation_id, tag_id)
);

attachments (
  id uuid primary key,
  obligation_id uuid not null references obligations(id),
  file_name text not null,
  storage_key text not null,        -- key/path in MinIO/S3-compatible storage
  mime_type text not null,
  size_bytes bigint not null,
  uploaded_at timestamptz not null default now(),
  deleted_at timestamptz
);

people (
  id uuid primary key,
  user_id uuid not null references users(id),
  name text not null,
  phone text,
  note text
);

assets (
  id uuid primary key,
  user_id uuid not null references users(id),
  name text not null,
  type text not null,               -- Vehicle, Device, Property, Appliance, Other
  metadata jsonb
);

activity_log (
  id uuid primary key,
  obligation_id uuid not null references obligations(id),
  user_id uuid not null references users(id),
  action text not null,
  changes jsonb,
  created_at timestamptz not null default now()
);
```

## 4. Indexes (minimum set — add more as query patterns emerge)

```sql
create index ix_obligations_user_status_due   on obligations (user_id, status, due_date);
create index ix_obligations_user_type          on obligations (user_id, type);
create index ix_obligation_occurrences_ob_due  on obligation_occurrences (obligation_id, due_date);
create index ix_reminders_obligation           on reminders (obligation_id);
create index ix_notification_log_reminder_sent on notification_log (reminder_id, sent_at);
create index ix_attachments_obligation         on attachments (obligation_id);
create index ix_activity_log_obligation        on activity_log (obligation_id, created_at);

-- search
create index ix_obligations_title_trgm on obligations using gin (title gin_trgm_ops);
-- requires: create extension if not exists pg_trgm;
```

Full-text search: Postgres `tsvector` over `title + notes`, with `pg_trgm` as an `ILIKE` fallback for stronger partial-match support on Persian text (per spec §20).

## 5. Migration policy

- EF Core Migrations, one migration per schema-affecting task — never bundle unrelated schema changes into one migration.
- Every migration must be reversible (implement `Down()` correctly, don't leave it as a stub) unless truly not feasible (document why in the migration file's comment if so).
- Migration naming: `<YYYYMMDDHHmm>_<ShortDescription>` (EF Core default timestamp + PascalCase description).
- Never edit an already-applied migration file — create a new migration to fix a mistake.
- Any migration must be captured in the task's plan file (`docs/plans/NNNN-slug.md`) per `AGENTS.md` §6, and this file (`DATABASE.md`) updated in the same task.

**Baseline:** `20260813115423_InitialCreate` exists as an intentionally empty baseline (ADR-0015) created in task 0002 — it locks in the provider/naming-convention pipeline before any business table. The first table (`users`) lands in 0003. The `__EFMigrationsHistory` table is created/managed by EF Core itself.

## 6. Money & currency

`extra_fields` may carry an `amount` (integer, Rial) and `currency` (default `IRT`) for financial obligation types (installment, debt, cheque, rent, bill, subscription — see `docs/DOMAIN.md` §4). Never store money as `float`/`double`/`numeric` with implicit rounding ambiguity — always integer Rial.

## 7. Multi-tenancy / data isolation

There's no `tenant_id` — isolation is per-`user_id`. Every table that stores user data has a direct or indirect `user_id` reference, and every Application-layer query must filter by the current user's `UserId` (see `AGENTS.md` §2 and `docs/SECURITY.md`). Integration tests must assert this explicitly (see `docs/TESTING.md`).
