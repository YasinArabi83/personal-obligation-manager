# 0004 — OTP auth endpoints + JWT issuance + rate limiting

- Status: **Done** (implemented 2026-08-13)
- Date: 2026-08-13
- Related task: PROGRESS.md `0004` — OTP auth endpoints (`auth/otp/request`, `auth/otp/verify`) + JWT issuance + basic rate limiting.

## Goal

Deliver the three auth endpoints specified in API.md §2 (`otp/request`, `otp/verify`, `refresh`) on top of the Identity plumbing from task 0003. Login is phone-number + OTP only (no password, ADR-0001). `verify` issues a short-lived access JWT plus a hashed, rotated, revocable refresh token; `refresh` rotates it. OTP delivery goes through the `ISmsSender` port (logging stub for now; real sms.ir is task 0017). Requests are rate-limited both per-IP (built-in middleware) and per-phone (in-memory limiter) per SECURITY.md §2.

## Scope (strict)

Only the work listed here. No Redis, no Domain `RefreshToken` aggregate, no controllers, no new token abstractions beyond `ITokenService`/`IOtpSender`/`IOtpVerifier`/`ISmsSender`, no real SMS provider (task 0017), no `/logout` (revoke-on-logout is a later task). Lifetimes are fixed at access = 15 min, refresh = 30 days.

## Affected modules/layers

- **Domain:** no changes. `RefreshToken` is an Infrastructure persistence model, NOT a Domain aggregate (see Risks Q3). `User` already has everything needed (0003).
- **Application:** new — the auth use-case service and the port interfaces (`IOtpSender`, `IOtpVerifier`, `ISmsSender`, `ITokenService`, `IOtpRateLimiter`). This layer currently only has `ApplicationMarker.cs`.
- **Infrastructure:** OTP + SMS + token + rate-limit implementations; `RefreshToken` persistence model + EF configuration; a new migration for `refresh_tokens`; JWT bearer + Identity wiring additions.
- **API:** minimal-API endpoints for the three routes; JWT bearer auth + rate-limit middleware registered in `Program.cs`.
- **Frontend:** none.

## Files to add/change

### Application (`src/Application/`)

- `Auth/Ports/IOtpSender.cs` — `Task<string> SendOtpAsync(string phoneNumber, CancellationToken ct)`; generates the code via the framework token provider (in the Infrastructure impl) and returns the code so the (logging) SMS sender can surface it. (See Q5: no fixed-code backdoor; the logging sender is dev-only.)
- `Auth/Ports/IOtpVerifier.cs` — `Task<Guid?> VerifyAsync(string phoneNumber, string code, CancellationToken ct)`; returns the user id on success or `null` on failure. Find-or-create happens here (see Q7).
- `Auth/Ports/ISmsSender.cs` — `Task SendAsync(string phoneNumber, string message, CancellationToken ct)` (matches ARCHITECTURE.md §3).
- `Auth/Ports/ITokenService.cs` — issues/rotates/revokes both tokens:
  - `(string accessToken, DateTimeOffset expiresAt) IssueAccessToken(Guid userId, string phoneNumber, string displayName)`
  - `Task<(string refreshToken, DateTimeOffset expiresAt)> IssueRefreshTokenAsync(Guid userId, CancellationToken ct)`
  - `Task<(string? accessToken, DateTimeOffset expiresAt)?> TryRotateRefreshTokenAsync(string refreshToken, CancellationToken ct)` — returns `null` if invalid/revoked/expired.
- `Auth/Ports/IOtpRateLimiter.cs` — `Task<RateLimitDecision> CheckOtpRequestAsync(string phoneNumber, CancellationToken ct)` and `CheckOtpVerifyAsync(...)`, returning an allow/deny + retry-after (see Q2).
- `Auth/AuthAppService.cs` — orchestrates `otp/request` (rate-check → send) and `otp/verify` (rate-check → verify → issue tokens). Thin; no business logic of its own. Token rotation for `refresh` is handled inline by the endpoint calling `ITokenService`.

### Infrastructure (`src/Infrastructure/`)

- `Identity/OtpSender.cs` — `IOtpSender`; uses `UserManager<ApplicationUser>` + `GenerateUserTokenAsync(user, PhoneNumberTokenProvider, "Phone(number)")` (the built-in provider wired in 0003) and hands the code to `ISmsSender`. Resolves the user via `UserManager.FindByNameAsync(phone)` (find-or-create happens in `OtpVerifier`, see Q7).
- `Identity/OtpVerifier.cs` — `IOtpVerifier`; find-or-create (Q7), `VerifyUserTokenAsync`, on success sets `PhoneNumberConfirmed = true` + `UpdateSecurityStampAsync`, returns the `Guid`.
- `Sms/LoggingSmsSender.cs` — `ISmsSender`; in dev/test logs/writes the message (including the code) to a structured log. **No fixed/constant OTP** (Q5). Registered only when SMS provider is "Logging".
- `Auth/Jwt/JwtTokenService.cs` — `ITokenService`; access token via `System.IdentityModel.Tokens.Jwt`; refresh token = random URL-safe bytes, SHA-256 hashed at rest, with `family_id`. Rotation revokes the whole family on reuse detection (Q1).
- `Auth/Jwt/RefreshToken.cs` — Infrastructure persistence model (see Schema changes).
- `Auth/Jwt/RefreshTokenConfiguration.cs` — EF `IEntityTypeConfiguration<RefreshToken>`.
- `Auth/RateLimiting/InMemoryOtpRateLimiter.cs` — `IOtpRateLimiter`; two sliding windows in a `ConcurrentDictionary` (Q2): per-phone OTP requests (≤3 / 10 min), per-phone failed verifies (≤5 / 1 h). In-memory only, single-VPS MVP.
- `Auth/RateLimiting/DependencyInjection.cs` — `AddOtpAuthInfrastructure(IConfiguration)` registers SMS/JWT/rate-limit impls. Named to avoid the `AddAuth` collision (0003 already uses `AddAuth` for Identity) — see Q1-naming.
- `Persistence/PomDbContext.cs` — add `DbSet<RefreshToken> RefreshTokens`.
- `Migrations/<timestamp>_AddRefreshTokens.{cs,Designer.cs}` — from `dotnet ef migrations add`.
- `Identity/DependencyInjection/IdentityServiceCollectionExtensions.cs` — extend the existing `AddAuth` (or a sibling method) to also `AddJwtBearer` + `AddRateLimiter` + read `Jwt:SigningKey`, `Jwt:AccessLifetime`, `Jwt:RefreshLifetime`. (Program.cs calls one composition method.)

### API (`src/Api/`)

- `Endpoints/AuthEndpoints.cs` — minimal-API extension registering the three routes. DTOs (`OtpRequest`, `OtpVerify`, `RefreshRequest`, `AuthResponse`) live here in the API layer, never exposing entities. Response shapes match API.md §2 exactly (Q4): `verify` → `{ accessToken, expiresAt, user:{id,phoneNumber,displayName} }`; `refresh` → `{ accessToken, expiresAt }`. **No `refreshToken` field in either body** (Q4-a).
- `Program.cs` — ensure JWT bearer + rate limiter are added; keep `GET /healthz` public; require auth on everything else.

## Schema changes

New table `refresh_tokens` (Infrastructure auth store, snake_case per ADR-0014):

| column | type | notes |
|---|---|---|
| `id` | uuid PK | app-assigned `Guid.NewGuid()` |
| `user_id` | uuid FK → `users(id)` | indexed |
| `family_id` | uuid | reuse-detection grouping; all tokens from a login share one family (Q1-a) |
| `token_hash` | bytea (via `byte[]`) | SHA-256 of the raw token; raw token never stored |
| `expires_at` | timestamptz | now + 30 days |
| `revoked_at` | timestamptz NULL | set on rotation/revocation/logout |
| `created_at` | timestamptz | default now() |

Reuse detection (Q1-a): on `refresh`, load the matching row by `token_hash`. If `revoked_at IS NOT NULL` → a previously-rotated token is being replayed → **revoke the entire family** (`UPDATE refresh_tokens SET revoked_at = now() WHERE family_id = ? AND revoked_at IS NULL`) and reject. Otherwise issue a new token in the same `family_id` and revoke the consumed one.

No change to `users`.

## Lifetimes / endpoints / misc (Q4)

- Access token: 15 min. Refresh token: 30 days. From configuration (`Jwt:AccessLifetimeMinutes`, `Jwt:RefreshLifetimeDays`) with those defaults.
- Endpoints exactly per API.md §2:
  - `POST /api/v1/auth/otp/request` `{phoneNumber}` → `204` (rate-limited → `429`)
  - `POST /api/v1/auth/otp/verify` `{phoneNumber, code}` → `200 {accessToken, expiresAt, user:{id,phoneNumber,displayName}}` / `401` on bad code / `429`
  - `POST /api/v1/auth/refresh` `{refreshToken}` → `200 {accessToken, expiresAt}` / `401`
- Error shape `{error:{code,message}}` everywhere.
- Minimal API, not controllers.

## Risks / open questions

- **Q1-a (resolved):** reuse detection via `family_id` column + family-wide revocation on replay (chosen over a self-ref `replaced_by` chain).
- **Q1-naming:** the 0003 extension method is named `AddAuth`. The 0004 token/SMS/rate-limit registration will live in a **separately-named** method (`AddOtpAuthInfrastructure`) to avoid collision; `Program.cs` calls both. (Alternative: fold everything into the existing `AddAuth`. Decision: keep them separate for readability; revisit if `Program.cs` gets noisy.)
- **Q2 (resolved):** hybrid rate limiting. Built-in `AddRateLimiter` partitions by client IP (fixed window / token bucket) as the coarse outer gate; the in-memory `IOtpRateLimiter` enforces SECURITY.md §2's per-phone windows (3 OTP/10 min, 5 failed verify/1 h) inside the app service, because the built-in middleware can't partition on the JSON-body phone number. Single-VPS MVP, no Redis.
- **Q3 (resolved):** `RefreshToken` is an Infrastructure persistence model, not a Domain aggregate. Consistent with "no invented features" and the fact that a refresh token is an auth/infra concern, not domain behavior. The Domain stays free of auth dependencies.
- **Q4 (resolved):** response shapes match API.md §2 exactly; the refresh token is delivered in the `verify`/`refresh` response as documented and is NOT echoed in the bodies beyond what the spec shows.
- **Q5 (resolved):** `LoggingSmsSender` logs the real generated code in dev/test only; there is **no** fixed/constant OTP and no backdoor. Production must be configured with the real provider (task 0017) so logs never carry a code.
- **Q6 (resolved):** port names follow ARCHITECTURE.md §3 — `IOtpSender` + `IOtpVerifier` (not a single `IOtpAuthenticator`), plus `ISmsSender` and `ITokenService`.
- **Q7 (resolved):** find-or-create happens in `OtpVerifier` on successful verification (a valid TOTP proves the phone). `OtpSender` only resolves/does-not-create. This keeps a successful OTP as the single creation trigger.
- **Q8 (resolved):** access 15 min, refresh 30 days, configurable.
- **Q9:** DDD boundary check — grep the diff before finishing to confirm no `Microsoft.AspNetCore.Identity` / `Microsoft.EntityFrameworkCore` / JWT `using` appears in Domain or Application.

## Acceptance criteria

- [ ] `otp/request` returns 204 and (dev) logs an OTP; `otp/verify` with the correct code returns 200 with `{accessToken, expiresAt, user}`.
- [ ] `otp/verify` with a wrong code returns 401; after 5 failed verifies for one phone within an hour, further verifies return 429.
- [ ] After 3 `otp/request` for one phone within 10 min, further requests return 429.
- [ ] `refresh` with a valid token returns a new `{accessToken, expiresAt}`; the consumed refresh token is revoked.
- [ ] Refresh-token reuse detection: replaying a consumed token revokes the whole family and rejects.
- [ ] Every endpoint except `auth/*` requires auth; IP rate limiting gates the auth routes.
- [ ] No password field anywhere; no fixed/constant OTP; `Jwt:SigningKey` read from env/config only (never committed).
- [ ] Unit tests: refresh-token rotation + reuse detection; rate-limit windows; `OtpVerifier` find-or-create.
- [ ] Integration tests (Testcontainers Postgres): full request → verify → refresh → rotate → replay flow; per-phone isolation of rate-limit state.
- [ ] Migration added and applies cleanly.
- [ ] Docs updated: API.md (confirm shapes — no change expected), DATABASE.md (refresh_tokens), SECURITY.md (note JWT lifetimes + reuse detection), DECISIONS.md (ADR for token lifetimes + refresh model + rate-limit strategy), PROGRESS.md, this file `Status: Done`.
