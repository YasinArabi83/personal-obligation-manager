# API.md — Personal Obligation Manager

> REST API reference. Source of truth for endpoints — update this file in the same task whenever an endpoint is added, removed, or its contract changes (`AGENTS.md` §7, Definition of Done).

---

## 1. Conventions

- Base path: `/api/v1/...`
- All responses: JSON.
- Standard error shape:
  ```json
  { "error": { "code": "string", "message": "string" } }
  ```
- Auth: `Authorization: Bearer <jwt>` header on every endpoint except `auth/*`.
- Every authenticated query/command is scoped to the current user's `UserId` server-side — never trust a `userId` from the request body/query for authorization.
- Pagination: `?page=&pageSize=` on list endpoints.
- Filtering: plain query params (no GraphQL/OData in MVP).
- Dates in request/response bodies: ISO 8601 UTC. Jalali conversion is a frontend/presentation concern only.
- Money in response bodies: integer Rial (matches the `bigint` storage — never a float).

---

## 2. Auth

No password anywhere. Flow: request OTP → verify OTP → receive JWT + refresh token.

```
POST   /api/v1/auth/otp/request
  Body: { "phoneNumber": "string" }
  → 204 No Content (OTP sent via SMS). Rate-limited — see docs/SECURITY.md.
  → 429 (otp_rate_limited) with Retry-After header when the per-phone window is exceeded.

POST   /api/v1/auth/otp/verify
  Body: { "phoneNumber": "string", "code": "string" }
  → 200 OK { "accessToken": "string", "expiresAt": "datetime", "refreshToken": "string", "user": { "id", "phoneNumber", "displayName" } }
  → 401 if code invalid/expired (otp_invalid_or_expired; OTP lifetime is framework-managed ~3–6 min — ADR-0016)
  → 429 (otp_rate_limited) after 5 failed verifies for the phone within an hour

POST   /api/v1/auth/refresh
  Body: { "refreshToken": "string" }
  → 200 OK { "accessToken": "string", "expiresAt": "datetime", "refreshToken": "string" }
  → 401 if the refresh token is invalid, expired, or already consumed (the consumed token and
         its whole family are revoked on a replay — ADR-0017)
```

The `refreshToken` returned by `verify` and `refresh` is **rotated on every use**: the consumed token is revoked and a new one issued in the same family (ADR-0017). Access tokens live 15 min, refresh tokens 30 days (configurable via `Jwt:AccessLifetimeMinutes` / `Jwt:RefreshLifetimeDays`).

## 3. Obligations

```
GET    /api/v1/obligations?status=&type=&category=&from=&to=&q=&page=&pageSize=
POST   /api/v1/obligations
GET    /api/v1/obligations/{id}
PUT    /api/v1/obligations/{id}
DELETE /api/v1/obligations/{id}                 -- soft delete

POST   /api/v1/obligations/{id}/complete
POST   /api/v1/obligations/{id}/postpone
       Body: { "newDueDate": "datetime" }
POST   /api/v1/obligations/{id}/skip
       Body: { "reason": "string?" }
POST   /api/v1/obligations/{id}/archive
POST   /api/v1/obligations/{id}/restore          -- undo soft delete, within retention window
```

`POST`/`PUT` bodies use a dedicated `ObligationDto` — never accept or return the domain entity directly. `ExtraFields` is validated server-side per `Obligation.Type` (see `docs/DOMAIN.md` §4).

## 4. Dashboard

```
GET    /api/v1/dashboard
  → { "today": [...], "thisWeek": [...], "thisMonth": [...], "overdue": [...], "upcoming": [...] }
  Aggregated, capped list per section (see spec §13 — a fixed max card count with a "view all" link on the frontend).
```

## 5. Recurrence & Occurrences

```
GET    /api/v1/obligations/{id}/recurrence
PUT    /api/v1/obligations/{id}/recurrence        -- create or update the RecurrenceRule
DELETE /api/v1/obligations/{id}/recurrence        -- stop recurring

GET    /api/v1/obligations/{id}/occurrences?from=&to=
```

## 6. Reminders

```
GET    /api/v1/obligations/{id}/reminders
POST   /api/v1/obligations/{id}/reminders
       Body: { "offsetType": "BeforeDue|OnDue|AfterDue", "offsetDays": int, "channel": "Sms|Email|BrowserPush" }
DELETE /api/v1/obligations/{id}/reminders/{reminderId}
```

## 7. Attachments

```
POST   /api/v1/obligations/{id}/attachments        -- multipart upload, max 10MB, max 5 per obligation
GET    /api/v1/obligations/{id}/attachments
GET    /api/v1/attachments/{attachmentId}/download-url   -- returns a short-lived signed URL
DELETE /api/v1/attachments/{attachmentId}            -- soft delete, 30-day recovery window
```

## 8. Categories & Tags

```
GET    /api/v1/categories
POST   /api/v1/categories
PUT    /api/v1/categories/{id}
DELETE /api/v1/categories/{id}

Category list returns system defaults (`userId = null`) plus the current user's own categories.
Names are trimmed and limited to 100 characters. System defaults and categories owned by another
user are intentionally reported as `404 not_found` on update/delete. Deleting a user category
referenced by one of that user's obligations returns `409 conflict`; obligations are never deleted
or silently detached.

GET    /api/v1/tags
POST   /api/v1/tags
DELETE /api/v1/tags/{id}
```

## 9. Person & Asset (should-have, add once scheduled)

```
GET/POST/PUT/DELETE  /api/v1/people
GET/POST/PUT/DELETE  /api/v1/assets
GET                   /api/v1/assets/{id}/obligations   -- related obligation history
```

## 10. Account

```
GET    /api/v1/account/me
PUT    /api/v1/account/me                -- display name, calendar preference
DELETE /api/v1/account/me                -- account deletion, GDPR-like
GET    /api/v1/account/export             -- JSON export of the user's data
```

## 11. Error codes (extend this table as new codes are introduced)

| Code | Meaning |
|---|---|
| `validation_error` | Request body failed validation |
| `not_found` | Resource doesn't exist or doesn't belong to the current user |
| `otp_invalid_or_expired` | OTP verification failed |
| `otp_rate_limited` | Too many OTP requests for this phone number |
| `unauthorized` | Missing/invalid JWT |
| `forbidden` | Valid JWT but not the resource owner |
| `conflict` | The requested mutation conflicts with existing references |
| `attachment_limit_exceeded` | More than 5 files, or file over 10MB |
