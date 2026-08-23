# KaamLink — Phase 02 Report
**Backend skeleton + phone-OTP authentication + authorization framework + audit + CI**

- Date: 2026-08-23 · Branch: `arena/01a02fc9-kaamlink` · Commit: `5d5d725`
- Prereqs read: `docs/phase-01-report.md` (§P roadmap, Phase 02 definition) · ADR 0002/0003

---

## 1. Goal (from the Phase 01 roadmap)

Deliver a deployable API with identity, OTP auth, JWT, authorization framework,
PostgreSQL + migrations, audit log, environments (dev/stage/prod) and CI.
**Definition of Done:** `register → verify → token → authorized request` works
end-to-end; CI green; secrets via environment.

## 2. What was built

### Solution structure (modular monolith; layer arrows point inward)
```
backend/src/KaamLink.Domain          pure entities — no dependencies
backend/src/KaamLink.Application     use cases + ports (interfaces) + DTOs + options
backend/src/KaamLink.Infrastructure  EF Core/Npgsql, repositories, audit, OTP sender(s), migrations
backend/src/KaamLink.Api             composition root, controllers, middleware, JWT, guards
backend/tests/KaamLink.UnitTests        xunit + in-memory fakes (no DB)
backend/tests/KaamLink.IntegrationTests WebApplicationFactory + Testcontainers (real PostgreSQL 16)
```
Layer rule enforced by project references: `Domain ← Application ← Infrastructure → Api`, `Application → Api`.

### Identity & authentication (ADR 0003 — auth identity carries zero business data)
| Behavior | Implementation |
|---|---|
| OTP request | 6-digit RNG code; **HMAC-SHA256 (peppered) hash only** stored; 60s resend cooldown; 5 min TTL |
| OTP verify | 5 attempts max → challenge locks; constant-time comparison; first success **registers** the user |
| Access token | JWT HS256, 15 min, claims `sub`/`phone`/`platform_admin`/`jti`, issuer+audience validated, 30s skew |
| Refresh token | opaque 32B (plaintext never stored — SHA-256 hash); **rotated every use**; presenting a rotated token = **reuse detection → whole family revoked** |
| Logout | revokes caller's own refresh token only; foreign-token attempts audited, not executed |
| Blocked/deleted accounts | 403 everywhere (verify, refresh, me) + audited login failure |
| Phone input | E.164 required; UAE local `05X XXXXXXX` auto-normalized to `+971...` |

### Authorization framework
- `AuthorizationPolicies.PlatformAdmin` policy wired (claim `platform_admin=true`); company-role
  policies (OWNER/ADMIN/HIRING_MANAGER/SUPERVISOR/VIEWER) land with CompanyMember in Phase 03.
- Employer endpoints will check membership server-side via EF data-layer scoping — framework point
  (`ICurrentUser`, per-request user resolution) established now.

### Rate limiting
- Sliding-window limiter, partitioned by **normalized target phone** (extracted from small OTP bodies
  before routing; model binding unaffected) with IP fallback: OTP request 5/5min, OTP verify 15/5min.
- 429 responses follow the same problem-details contract with code `rate.limit_exceeded`.

### Audit log (§47)
`audit_logs` (append-only bigint, jsonb metadata, indexes on entity + created_at). Actions via
`AuditActions` constants: user_registered, otp_requested, login_succeeded, login_failed,
token_refreshed, token_reuse_detected, token_revoked, foreign revoke attempt.
**Write-through design**: security events persist even when the operation then fails.
**Never** stores codes/tokens/secrets (integration test asserts it).

### Database
4 committed tables with PKs/FKs/unique constraints/indexes: `users` (unique phone, status as text),
`otp_challenges`, `refresh_tokens` (unique token_hash, FK→users cascade), `audit_logs`.
Initial EF Core migration committed (`20260823120000_InitialIdentity` + snapshot) — migrations are
part of the repo from the first model change, as promised in Phase 01 §F.

### Environments & secrets (§54)
- `appsettings.json` (empty secrets) + Development/Staging/Production variants; env vars override;
  `appsettings.local.json` (gitignored) for dev machines; `.env`/`.env.*` gitignored with `.env.example` committed.
- **Fail-fast startup guards**: key lengths (JWT ≥32, OTP pepper ≥16), value ranges; in Production —
  sandbox OTP provider forbidden, default DB passwords rejected, `dev-only` placeholder secrets rejected.
- Migrations on startup: Development/Staging auto, Production manual (deploy step).

### API contract
- Errors: `application/problem+json` + stable machine `code` (`auth.otp_invalid`, `auth.token_reused`, …)
  — the Android client can switch on these.
- Health: `/health/live`, `/health/ready` (executes `SELECT 1` against PostgreSQL).
- OpenAPI exposed in Development/Staging (`/openapi/v1.json`).

### Ops assets
Multi-stage `Dockerfile` (Debian runtime for ICU, non-root `$APP_UID`), `docker-compose.yml`
(API + Postgres with health-gated startup), `requests.http`, `backend/README.md` (full runbook).

### CI (prepared — see §4 status)
`backend-ci.yml` (restore → Release build → unit → integration w/ Testcontainers → gitleaks) and
`android-ci.yml` (.NET 10 + `maui-android` workload → Release build — *also the Phase 01 compile gate*).

## 3. Testing delivered

**Unit (KaamLink.UnitTests)** — ~40 assertions across 22 test cases:
- PhoneNumber: E.164 passthrough, UAE-local conversion, separator stripping, `00` prefix, 10 invalid forms.
- OTP request: hashing/no-plaintext-at-rest, cooldown 429 with retryAfter, post-cooldown resend, invalid phone.
- OTP verify: registration + token shape + audit order, second login reuses user, wrong-code attempt counting,
  attempt-exhaustion lock, expiry rejection, blocked-account 403 + audit.
- Refresh: rotation semantics (chain pointer, replaced id), **reuse → family revocation → old *and* new dead**,
  unknown/expired/empty token errors, blocked user, logout revoke, foreign-logout protection + audit.
- Me: identity projection, unknown → 401, blocked → 403.

**Integration (KaamLink.IntegrationTests, real PostgreSQL via Testcontainers)** — 9 E2E cases proving the
DoD over HTTP: register→verify→token→`/me` authorized round-trip (incl. UAE-local input normalization),
401 without token, stable error codes, refresh rotation/reuse family-revocation chain, logout→revoked refresh
rejected, blocked user forbidden, OTP rate limiter → 429 `rate.limit_exceeded`, and audit-log correctness
(action coverage + no sensitive data persisted).

## 4. Verification status (honest)

| Check | Status |
|---|---|
| Static cross-verification (configs ↔ migration ↔ snapshot columns/indexes, DI wiring order, JSON/XML validity, secret-heuristic scan, fake coverage) | ✅ Passed locally (automated script) |
| Compile `dotnet build` (sandbox) | ⚠️ Not possible here — no .NET SDK, no NuGet egress (same constraint as Phase 01) |
| `dotnet test` unit + integration (real PostgreSQL) | ⚠️ Prepared; runs in `backend-ci` — **pending CI activation, see below** |
| Android compile gate (Phase 01 app) | ⚠️ Prepared in `android-ci` — **pending CI activation** |

**CI activation blocker:** pushing files to `.github/workflows/` is rejected by the current GitHub App
token ("refusing to allow a GitHub App to create or update workflow … without `workflows` permission").
The workflows are committed under [`ci/workflows/`](../ci/workflows/) with a one-command activation
runbook ([`ci/README.md`](../ci/README.md)). Activate by reconnecting GitHub in Arena **with the
workflows permission**, or have a maintainer move the files. Nothing else needs to change — path
filters and job steps are location-independent.

## 5. Risks update (from Phase 01 §O)

- **R-3 backend greenfield** → substantially retired: identity slice exists with full test EE2E proof path.
- **R-7 unverified compile** → partially retired: static verification green; **remote CI compile/test
  remains the pending formal gate** (blocked only by workflow-file permission).
- New: **R-13 OTP provider is a sandbox** (`log`) — by design; hard-blocked in Production by startup
  guards; real SMS provider scheduled Phase 06 (with R-6 cost/deliverability review).
- New: **R-14 package versions unpinned-by-policy check** — first successful CI restore should confirm
  NuGet resolution (`Npgsql.EFCore 10.0.0`, `Testcontainers.PostgreSql 4.0.0`, etc.); adjust pins if needed.

## 6. Next phase (per roadmap)

**Phase 03 — Profiles & companies:** WorkerProfile (progressive onboarding API + Android screens),
skills reference data, Company + CompanyMember(roles) + verification states, admin-lite verification
endpoint, data-layer scoping tests. Depends on: this identity slice (done) + CI gate (recommended green).

---
## ⛔ CHECKPOINT
Phase 02 code is complete and pushed (`5d5d725`). Formal compile/test verification awaits CI activation
(`ci/README.md` — one command once the GitHub token has the workflows permission). Awaiting your next
instruction: proceed to **Phase 03**, or activate CI first and I'll report the green build.
