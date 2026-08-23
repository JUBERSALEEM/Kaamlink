# KaamLink Backend

ASP.NET Core 10 modular monolith behind versioned REST (`/api/v1/**`) — see
`docs/phase-01-report.md` §E and ADR 0002. Phases so far: **02** identity + phone-OTP
auth + authorization framework + audit log + rate limiting + CI; **03** worker profiles
(Skill Passport), company onboarding + admin verification-lite, seeded occupation catalog.

## Layout

```
backend/
  src/KaamLink.Api/             Composition root, controllers, middleware, JWT plumbing, guards
  src/KaamLink.Application/     Use cases (AuthService), ports (interfaces), DTOs, options
  src/KaamLink.Domain/          Pure entities (User, OtpChallenge, RefreshToken, AuditLog, PhoneNumber)
  src/KaamLink.Infrastructure/  EF Core + Npgsql, repositories, audit, OTP sender(s), migrations
  tests/KaamLink.UnitTests/     xunit, in-memory fakes (no DB)
  tests/KaamLink.IntegrationTests/  WebApplicationFactory + Testcontainers (real PostgreSQL)
  Dockerfile  docker-compose.yml  .env.example  requests.http
```

**Layer rule:** `Domain ← Application ← Infrastructure` and `Application ← Api`,
`Infrastructure ← Api`. Business rules never live in controllers; persistence never
leaks into Application.

## Auth model (Phase 02)

```
POST /api/v1/auth/otp/request   { phone }                    → 6-digit code (rate-limited per phone)
POST /api/v1/auth/otp/verify    { phone, code }              → token pair (registers on first login)
POST /api/v1/auth/token/refresh { refreshToken }             → rotated pair; reuse revokes the family
POST /api/v1/auth/logout        { refreshToken }  (Bearer)   → revokes that refresh token
GET  /api/v1/auth/me                              (Bearer)   → identity + worker progress + memberships
GET  /health/live | /health/ready                            → probes
```

## Profiles, companies & admin-lite (Phase 03)

```
GET  /api/v1/skills/catalog                       (Bearer)   → seeded bilingual occupation catalog
GET  /api/v1/workers/me                           (Bearer)   → own profile (404 worker.profile_not_found if none)
PUT  /api/v1/workers/me              { patch }    (Bearer)   → progressive onboarding patch (creates on first call)
PUT  /api/v1/workers/me/skills       { skills[] } (Bearer)   → full replacement of self-declared skills
POST /api/v1/companies               { … }        (Bearer)   → register company (starts Pending; caller = Owner)
GET  /api/v1/companies/mine                       (Bearer)   → own memberships (membership-scoped)
GET  /api/v1/admin/companies/pending              (Bearer+)  → review queue — PlatformAdmin policy
POST /api/v1/admin/companies/{id}/review { approve, reason? }(Bearer+) → Verified / Rejected(+reason)
```

- **Skill Passport (V1):** skills are *self-declared* by the worker with years of experience;
  `verificationState` starts at `SelfDeclared` and can later be raised by employer feedback
  or paid practical assessments (Phase 05+) — never by the worker, never implied in UI (§50).
- **Completeness/onboarding:** computed + persisted server-side on every patch
  (name 20 + primary skill 20 + emirate 15 + availability 15 + any skill 15 + salary pair 10
  + about 5 = 100). Clients render the stored value; they never define "complete" locally.
- **Company verification:** new companies are `Pending`. Only platform admins
  (`platform_admin=true` JWT claim, `PlatformAdmin` policy) can review; rejection requires a
  reason; nothing is ever presented as "verified" without a review record (§16, §50).
- **Catalog:** 6 categories / 20 curated UAE-practical occupations, seeded idempotently with
  fixed GUIDs (bilingual EN/AR). `requiresCertification` is set only for *Light Vehicle Driver*
  (unambiguous licence); everything else stays false pending professional legal review —
  we do not invent UAE legal requirements (§1/§L).
- **Admin bootstrap (dev):** phones in `Admin:BootstrapPhones` get `IsPlatformAdmin=true`
  **at registration only**. Dev default: `+971500000000` (appsettings.Development.json).
  Production grants admin access via a controlled operational procedure, not config edits.

- Access token: JWT (HMAC-SHA256), 15 min. Claims: `sub`, `phone`, `platform_admin`, `jti`.
- Refresh token: opaque, 30 days, **rotated on every use**; only SHA-256 hashes are stored;
  presenting a rotated token = reuse detection → whole family revoked.
- OTP codes: only HMAC-SHA256 (peppered) hashes stored; 5 attempts max; 60s resend cooldown;
  5 min lifetime. **Plaintext codes and tokens are never logged or audited (§47).**
- Error contract: `application/problem+json` + stable machine `code` (e.g. `auth.otp_invalid`).

## Configuration (env-specific secrets only — §54)

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings__Postgres` | env | Required. Production rejects default dev credentials. |
| `Auth__Jwt__SigningKey` | env | Required, min 32 chars. Production rejects `dev-only` placeholders. |
| `Otp__HashingKey` | env | Required, min 16 chars. |
| `Otp__Provider` | env | `log` (dev sandbox) — **forbidden in Production** by startup guards. |
| `Database__ApplyMigrationsOnStartup` | env/appsettings | Default: true in Development/Staging, false in Production. |
| `Admin__BootstrapPhones__0` | env/appsettings | Dev-only platform-admin registration phone(s). Never rely on this in Production. |
| `ASPNETCORE_ENVIRONMENT` | env | `Development` / `Staging` / `Production`. |

Machine-local overrides: `backend/src/KaamLink.Api/appsettings.local.json` (gitignored)
or `dotnet user-secrets` (Development only).

## Run locally

**Option A — everything in Docker (recommended):**
```bash
cd backend
cp .env.example .env            # dev placeholders are fine locally
docker compose up --build       # API on http://localhost:8080 (OTP codes appear in API logs)
```

**Option B — .NET CLI + local PostgreSQL:**
```bash
cd backend
docker compose up -d postgres   # just the database
cd src/KaamLink.Api
dotnet user-secrets set "Auth:Jwt:SigningKey" "dev-only-signing-key-0123456789abcdef"
dotnet user-secrets set "Otp:HashingKey" "dev-only-hashing-key-0123"
dotnet run                      # migrations apply automatically in Development
```

Then either use `requests.http` (VS/Rider HTTP client) or:
```bash
curl -s -X POST http://localhost:8080/api/v1/auth/otp/request -H 'Content-Type: application/json' -d '{"phone":"+971501234567"}'
# read the code from the API logs (Otp:Provider=log), then:
curl -s -X POST http://localhost:8080/api/v1/auth/otp/verify  -H 'Content-Type: application/json' -d '{"phone":"+971501234567","code":"<CODE>"}'
curl -s http://localhost:8080/api/v1/auth/me -H "Authorization: Bearer <ACCESS_TOKEN>"
```
OpenAPI JSON (Development/Staging): `http://localhost:8080/openapi/v1.json`

## Database migrations

Model changes must come with a migration (committed):
```bash
cd backend
dotnet tool install --global dotnet-ef   # once per machine
dotnet ef migrations add <Name> --project src/KaamLink.Infrastructure --startup-project src/KaamLink.Api
dotnet ef database update --project src/KaamLink.Infrastructure --startup-project src/KaamLink.Api
```
Verify the model and migrations are in sync (must print "no changes"):
```bash
dotnet ef migrations has-pending-model-changes --project src/KaamLink.Infrastructure --startup-project src/KaamLink.Api
```

## Tests

```bash
dotnet test tests/KaamLink.UnitTests/KaamLink.UnitTests.csproj                 # fast, no DB
dotnet test tests/KaamLink.IntegrationTests/KaamLink.IntegrationTests.csproj   # needs Docker (Testcontainers spins PostgreSQL)
```

CI (`.github/workflows/backend-ci.yml`) runs both suites + a secret scan on every
backend change; `.github/workflows/android-ci.yml` compiles the Android app on
every client change.

## Deploying (staging/production)

1. Build + push the image: `docker build -t <registry>/kaamlink-api:<tag> backend`
2. Provide env vars per the table above (real secrets, environment-specific).
3. Apply migrations as a deploy step (Production does **not** auto-migrate):
   `dotnet ef database update` or an EF migration bundle.
4. Run the container; expose behind TLS; point health probes at `/health/ready`.
5. **Blocked until then:** real SMS provider for OTP (current: `log` sandbox) — Phase 06.
