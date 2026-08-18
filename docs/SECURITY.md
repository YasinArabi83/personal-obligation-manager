# SECURITY.md — Personal Obligation Manager

> Threat model, secrets handling, and the backup/recovery runbook. Keep this current — it's the first file to check when something goes wrong or before any auth/storage/notification change.

---

## 1. Threat model (MVP scope)

| Asset | Threat | Mitigation |
|---|---|---|
| User's obligation data (financial/legal, sensitive) | Cross-user data leakage | Every query filtered by `UserId` server-side (never trust a client-supplied id); integration tests explicitly assert isolation (see `docs/TESTING.md`) |
| Phone number / OTP flow | OTP brute-force, SMS bombing / cost abuse | Rate-limit `POST /api/v1/auth/otp/request` per phone number and per IP; framework-managed OTP lifetime (~3–6 min via Identity's built-in `PhoneNumberTokenProvider` — ADR-0016); lock out after N failed verify attempts |
| JWT | Token theft / replay | Short access-token lifetime (15 min) + hashed, rotated refresh tokens with family-based reuse detection (ADR-0017); HTTPS enforced everywhere (no plaintext transport); tokens never logged |
| Attachments | Unauthorized file access | Files are private in MinIO/S3-compatible storage; access only via short-lived signed URLs or authenticated API calls, never public bucket listing |
| Database | Unauthorized direct access | Postgres never exposed publicly — only reachable inside the Docker Compose network; strong generated password via secret, not committed |
| Secrets (DB password, JWT signing key, SMS provider API key, MinIO keys) | Leakage via source control or logs | All secrets via environment variables / `.env` (git-ignored) or Docker secrets — never hardcoded, never logged |
| External SMS/storage provider outage or sanctions-related filtering | Service disruption | Every external dependency sits behind a provider-agnostic interface (`ISmsSender`, `IFileStorage`) so the concrete provider can be swapped without touching domain/application code (see `AGENTS.md` §2, ADR-0002 in `docs/DECISIONS.md`) |
| Single domestic VPS/provider outage (chosen deploy target — ADR-0009) | Total service unavailability, or backup loss if backups sit on the same account | Off-site backups stored with a different provider/account than the primary VPS (§5); provider-agnostic design means migrating to a different VPS is a redeploy, not a rewrite |

## 2. Auth security details

- No password exists anywhere in the system — see `docs/ARCHITECTURE.md` §5 for the OTP flow.
- OTP token lifetime is framework-managed via Identity's built-in `PhoneNumberTokenProvider` (~3–6 min, not precisely configurable — ADR-0001/ADR-0016). Each successful verify rotates the `SecurityStamp`, making a code single-use.
- Rate limiting is **hybrid** (ADR-0017): a coarse per-IP fixed window (30/min, built-in `AddRateLimiter` on `auth/*`) plus an in-memory per-phone window inside `IOtpRateLimiter` (the middleware can't partition on a JSON-body phone):
  - Max 3 OTP requests per phone number per 10 minutes.
  - Max 5 failed verify attempts per phone number per hour before a temporary lockout.
- JWT: short-lived access token (15 min) + hashed, rotated, revocable refresh token (30 days). Refresh tokens are random URL-safe bytes stored only as a SHA-256 hash (`refresh_tokens.token_hash`), grouped by `family_id`; replaying a consumed token revokes the entire family and rejects (ADR-0017). `POST auth/refresh` rotates the token and returns the new one.
- `PhoneNumberConfirmed` only flips to `true` after the first successful OTP verification for that number.

## 3. Secrets management

- Local development: `.env` file, listed in `.gitignore`, with a committed `.env.example` showing required variable names (no real values).
- Production: environment variables injected via the deployment mechanism (Docker Compose `env_file` pointed at a non-committed file, or the host's secret manager if one is introduced later) — never baked into an image layer.
- Required secrets (keep this list current):
  - `POSTGRES_PASSWORD`
  - `JWT_SIGNING_KEY`
  - `SMS_PROVIDER_API_KEY` (e.g. sms.ir)
  - `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD`
  - `MINIO_ACCESS_KEY` / `MINIO_SECRET_KEY` (application's own credentials, distinct from root)

## 4. Data protection

- Transit: HTTPS enforced everywhere. TLS is terminated by the `reverse-proxy` service (Caddy) defined in `docker-compose.yml`, which obtains and renews a Let's Encrypt certificate automatically for `DOMAIN` (set in `.env`). `api` and `web` are not published on the host directly — only reachable through `reverse-proxy` (ports 80/443). Port 80 stays open only for the Let's Encrypt HTTP-01 challenge and to redirect to HTTPS.
- At rest: no sensitive data is stored that doesn't need to be (per spec §9, minimize unnecessary sensitive data). Financial figures are ordinary obligation data, not payment-card data — no PCI scope.
- Soft delete + short recovery windows (30 days for attachments) rather than hard delete, except account deletion, which is a genuine hard delete of personal data (GDPR-like, per spec §8/§10) after the user confirms.

## 5. Backup & recovery

- **Backup:** nightly `pg_dump` of the Postgres database, plus a backup of the MinIO bucket contents (or rely on MinIO's own versioning/replication once configured). Since production runs on a single domestic VPS (ADR-0009), backups must be pushed off that VPS — e.g. to a different provider's object storage, or synced to a second small VPS — so a single-provider outage or account issue doesn't also destroy the backups.
- **Retention:** keep at least 7 daily backups and 4 weekly backups (adjust once storage cost is evaluated).
- **Recovery runbook:**
  1. Provision a fresh Postgres instance (or reuse the existing one if only data is lost, not infrastructure).
  2. Restore the latest good `pg_dump`: `pg_restore -d <database> <dump_file>` (or `psql` for a plain-text dump).
  3. Restore MinIO bucket contents from the corresponding backup snapshot, matching the dump's timestamp as closely as possible so `attachments.storage_key` references stay valid.
  4. Verify: run a smoke test — log in via OTP, list obligations for a known test user, download a known attachment.
  5. Document actual recovery time and any issues encountered directly in this file's changelog (§7) after a real recovery event, so the runbook improves over time.
- This runbook has not yet been tested end-to-end — **the first Phase 0 task that touches deployment should include a dry-run of this recovery process** and update this section with what was learned.

## 6. Authorization checklist (apply to every new endpoint)

- [ ] Endpoint requires a valid JWT (unless it's `auth/*`)
- [ ] Every query/command derives `UserId` from the token, never from the request body/query string
- [ ] Ownership is checked before returning or mutating a resource (404, not 403, when the resource belongs to someone else — avoid leaking existence)
- [ ] No secret, token, or PII is logged

## 7. Changelog

| Date | Change |
|---|---|
| Initial | Threat model and runbook drafted alongside the rest of the doc set. Deploy target still undecided — TLS termination approach and off-site backup location to be finalized once a hosting decision is made. |
| Update | Deploy target finalized as a domestic Iranian VPS (ADR-0009). TLS termination via Caddy reverse proxy added to `docker-compose.yml`; off-site backup requirement made concrete (different provider/account than the primary VPS). Recovery runbook (§5) still needs a real dry run once the specific VPS and off-site backup destination are provisioned. |
| 0004 | Auth endpoints implemented (ADR-0017): JWT access tokens (15 min) + hashed/rotated refresh tokens (30 days) with family-based reuse detection; hybrid rate limiting (per-IP + per-phone) wired; OTP lifetime note reconciled to Identity's framework-managed window (ADR-0016). `.env.example` committed (git-ignored `.env` is the local copy). |
