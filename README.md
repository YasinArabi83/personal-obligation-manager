# Personal Obligation Manager

A personal system that tracks every obligation — task, payment, document renewal, subscription, contract, debt, maintenance, appointment — with native Jalali calendar and Toman/Rial support, built for the reality of personal administrative/financial life in Iran.

Not a Todo app (obligations carry status, history, attachments, and links to people/assets). Not an accounting app (no double-entry, no bank connections). See `Personal-Obligation-Manager-Spec.md` for the full product rationale.

## Documentation map

Read `AGENTS.md` first if you're an AI coding agent — it's the entry point and points to everything else.

| File | Contents |
|---|---|
| `AGENTS.md` | Rules and workflow for AI coding agents working on this repo |
| `docs/ARCHITECTURE.md` | Layers, DDD boundaries, dependency rules |
| `docs/DOMAIN.md` | Full domain model — entities, fields, enums, relationships |
| `docs/API.md` | Endpoint reference |
| `docs/DATABASE.md` | Schema, indexes, migration policy |
| `docs/SECURITY.md` | Threat model, secrets handling, backup/recovery runbook |
| `docs/TESTING.md` | Testing strategy and coverage targets |
| `docs/DECISIONS.md` | Technical decision log (ADRs) |
| `docs/PROGRESS.md` | Task status by phase |
| `docs/plans/` | One plan file per implemented task |

## Tech stack

- Backend: ASP.NET Core (.NET 10), EF Core, PostgreSQL 18
- Auth: ASP.NET Core Identity + `PhoneNumberTokenProvider` — phone number + OTP, no password
- Frontend: React + TypeScript + Vite, Tailwind CSS (RTL)
- File storage: MinIO (S3-compatible) behind `IFileStorage`
- SMS: Iranian provider (e.g. sms.ir) behind `ISmsSender`
- Architecture: Modular Monolith, DDD (Domain / Application / Infrastructure / API)
- Repository: monorepo (backend + frontend together)

## Running locally

Requires Docker and Docker Compose.

```bash
cp .env.example .env   # fill in real values — never commit .env
docker compose up --build
```

This brings up:
- `postgres` — PostgreSQL 18
- `minio` — S3-compatible object storage for attachments
- `api` — the ASP.NET Core backend (not published directly — reachable via `reverse-proxy`)
- `web` — the React frontend, served via Nginx (not published directly — reachable via `reverse-proxy`)
- `reverse-proxy` — Caddy, terminates TLS and routes `/api/*` to `api` and everything else to `web`

Default local ports and service names are defined in `docker-compose.yml`.

## Deployment

Production runs on a domestic (Iranian) VPS (see ADR-0009 in `docs/DECISIONS.md`) using the same `docker-compose.yml`. Set `DOMAIN` in `.env` to the real domain so Caddy can obtain a Let's Encrypt certificate automatically. See `docs/SECURITY.md` for the backup/recovery runbook — backups must be stored off the primary VPS.

## Contributing

See `CONTRIBUTING.md` for the git workflow, commit convention, and code review process.
