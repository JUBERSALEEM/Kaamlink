# KaamLink — Phase 03 Report

**Phase:** 03 — Onboarding (both sides) + first-boot readiness
**Date:** 2026-08-23 · **Branch:** `arena/01a02fc9-kaamlink`
**Status:** **BUILD-READY for first boot** 👉 [first-boot-guide.md](first-boot-guide.md)

---

## 1. Goal (from the Phase 02 roadmap)

Give both audiences a complete, persistent onboarding journey and make the app
tangible: **role selection → phone OTP sign-in → guided onboarding → home**,
with the backend as the single source of truth for identity, worker profiles
(Skill Passport V1), companies and their verification state — and the exact
instructions for the team to build and do a first-boot test (the question the
product owner asked).

## 2. What was built

### 2a. Backend — profiles, companies, catalog, admin-lite

- **Domain** (`KaamLink.Domain`): `WorkerProfile`, `WorkerSkill`,
  `SkillCategory`/`Skill` (reference data), `Company`, `CompanyMember`,
  enums `Emirate` (7), `Availability` (4), `BusinessCategory` (6),
  `CompanyRole` (5), `CompanyVerificationState` (3), `SkillVerificationState`.
- **Completeness is a server concept.** `WorkerProfile.RecalculateProgress` computes
  and persists both the score (name 20 + primary skill 20 + emirate 15 +
  availability 15 + any skill 15 + salary pair 10 + about 5 = 100) and the canonical
  `OnboardingComplete` flag (name + primary skill + emirate + availability + ≥1 skill).
  Clients never define "complete" locally (§50).
- **Skill Passport (V1)** — self-declared skills with years of experience;
  `verificationState` starts at `SelfDeclared`. Nothing and nobody marks a skill
  verified in V1; employer/assessment verification is a later phase (§16).
  Education is deliberately *not a field* — practical ability first.
- **Company onboarding + verification-lite** — creator becomes `Owner` (max 3 owned
  companies/user, `company.too_many`); companies start `Pending`; platform admins
  approve (`Verified`) or reject (`Rejected` + required reason) through a
  `PlatformAdmin`-policy endpoint. Re-review is rejected (`company.already_reviewed`).
  Unique trade-licence number via a PostgreSQL *filtered* unique index
  (`WHERE trade_license_number IS NOT NULL` — nullable licences don't collide).
- **Seeded occupation catalog** — 6 categories / 20 curated UAE-practical occupations,
  bilingual EN/AR, fixed GUIDs, idempotent startup seeding (runs in every environment).
  `requiresCertification=true` only for *Light Vehicle Driver* (unambiguous licence);
  everything else is `false` pending **professional legal review** — we do not invent
  UAE requirements (§1/§L).
- **Identity summary for routing** — `verify`, `refresh` and `/me` return
  `UserSummary { worker: {hasProfile, onboardingComplete, completeness}, memberships[] }`
  so the app routes (home vs onboarding) from *server* state. `IsPlatformAdmin` is
  granted at **registration only** to phones in `Admin:BootstrapPhones`
  (dev: `+971500000000`); config edits never mutate existing users.
- **New endpoints** (all versioned, all Bearer except OTP): `GET skills/catalog`,
  `GET/PUT workers/me`, `PUT workers/me/skills`, `POST companies`, `GET companies/mine`,
  `GET admin/companies/pending`, `POST admin/companies/{id}/review`.
  See the full annotated list in `backend/README.md` + runnable `backend/requests.http`.
- **Migration #2** `20260823130000_Phase03ProfilesCompanies` (+Designer, regenerated
  snapshot) — 6 new tables, composite keys where natural (`worker_skills`,
  `company_members`), cascade/restrict rules matching domain ownership, snake_case
  columns, enum-as-text converters.

### 2b. Android app — the full first-boot flow

- **`Core/Api`** — the app's entire network layer: `ApiConfig` (single URL source;
  DEBUG = emulator loopback `http://10.0.2.2:8080`), mirror DTOs with
  **source-generated JSON** (trim/AOT-safe), `ApiClient` (methods map 1:1 to
  endpoints; errors become `ApiException` carrying the backend's stable problem
  `code`), `SecureStorageTokenStore` (Android Keystore), `SessionService`
  (session state, single-flight refresh rotation, logout, identity reload — owns a
  plain HttpClient for auth calls so `ApiClient → auth handler → session` cannot
  cycle), `AuthHeaderHandler` (attach bearer; on 401 refresh once + retry once).
- **Auth feature** — phone login (accepts `050…`, `5…`, `9715…`, `+9715…`, all
  re-validated server-side) → OTP screen with verify + cooldown'd resend. Errors map
  to localized, honest messages (offline vs invalid code vs rate-limited).
- **Worker onboarding** — 3 steps, each persisted via API as it completes (a crash
  loses nothing): basics (name, profession via two-level catalog pickers, emirate,
  area) → skills (main profession pre-added, add/remove with years) → preferences
  (availability, optional salary range with cross-validation, about). **The flow only
  ends when the server confirms `onboardingComplete= true`.**
- **Worker home** — live profile from the API: name, ★ primary profession · emirate,
  completeness bar (server value), Skill Passport rows with "Self-declared" badges,
  availability/salary/about, plus the *explicit* note that job discovery lands next
  phase. Switch-to-employer + sign-out.
- **Employer onboarding** — company registration with up-front "reviewed before
  Verified" copy; **Employer home** — company card(s) with the real verification
  badge (Pending amber / Verified green / Rejected red), rejection reason when
  present, role, refresh-on-appear, switch + sign-out.
- **Navigation** — all seven pages are top-level Shell routes; every navigation is
  absolute (`//route`) so login/onboarding can never linger on a back stack. Role
  gating lives in ONE place (`RoleNavigator`) driven by `CurrentUser` from the server.
- **Session lifecycle** — stored session restored on app start (rotate → reload
  `/me`); expired sessions (401 even after silent refresh) route back to the role
  gate; logout revokes server-side best-effort and clears the Keystore.
- **Platform hardening** — `network_security_config.xml`: cleartext HTTP only to
  `10.0.2.2`/`localhost` (dev machine, incl. `adb reverse`); TLS required everywhere
  else. Release API URL intentionally unset until environments exist (Phase 06).

## 3. Testing delivered (50 backend tests total)

| Suite | Count | Highlights |
|---|---|---|
| Unit (fakes, no DB) | 35 (12 new) | **New:** `WorkerProfileServiceTests` (6): progressive onboarding reaching exactly 100 → complete; partial patches never wipe earlier steps; unknown skill / salary cross-validation / invalid enums → stable codes; honest 404. `CompanyServiceTests` (6): pending + owner on create; approve → verified (+audit); rejection requires reason; double review rejected; queue is pending-only oldest-first; 4th owned company rejected. Phase-02 suites (23) untouched |
| Integration (WebApplicationFactory + Testcontainers PostgreSQL) | 15 (6 new) | **New `OnboardingFlowTests` (6):** worker E2E over HTTP (catalog → 55% → 70% → 100%, `/me` reflects completion; skills all `SelfDeclared`); catalog requires auth + seeds intact (only the driver needs certification); employer E2E (create → Pending → admin queue → approve → Verified, `/me` memberships drive app gating); rejection reason visible to employer + queue drains; **non-admin → 403 on both admin endpoints**; membership/profile scoping. Phase-02 `AuthFlowTests` (9) untouched; the factory now also pins the admin bootstrap phone |

`run: cd backend && dotnet test` (integration tests self-provision a PostgreSQL container).

## 4. Verification status (honest)

- ❌ **Not compiled here** — this environment has no .NET SDK and no NuGet egress
  (unchanged from Phases 01–02). Code was authored compile-correct and reviewed
  line-by-line against API surfaces it calls; the first `dotnet restore` +
  `dotnet build` / CI run is the formal verification gate ([first-boot-guide §0](first-boot-guide.md)).
- ✅ **Static cross-verification** — `tools/verify-phase03.py` (committed, repeatable):
  **93 checks, 0 failures** — XML/JSON validity; migration ↔ snapshot ↔ EF-config
  column/index/filter coherence; seeder GUID/name/certification coherence;
  AuthService ctor ↔ test-harness arity (13); DI registrations; route ↔ AppRoutes
  parity; **resx ↔ designer ↔ XAML 3-way string-key parity (85 keys × EN/AR)**;
  StaticResource resolution; XAML↔VM binding typo net; DTO ↔ backend contract
  camelCase spot-mirror; Android manifest ↔ network-config wiring.
  - Real bugs caught by this pass and fixed before commit: an unresolvable
    `Shared.Emirate` generic in `WorkerProfileConfiguration`, a compile-breaking
    leftover in `CompanyServiceTests`, invalid business-category/skill names in
    integration tests, and `refresh` returning a degraded identity summary (would
    have misrouted the app after silent token refresh).
- ✅ Test doubles mirror real implementations (`InMemorySkillCatalogRepository`,
  `SeedTestCatalog` mirrors seed names incl. Arabic).
- ✅ **All package pins verified against the live nuget.org index** (every referenced
  version exists; restore failures on a dev machine point at SDK/feed/workload, not pins).
  Repo additionally ships `NuGet.config` (explicit gallery) + `global.json` (.NET 10 gate).
- ✅ Pushed to `arena/01a02fc9-kaamlink` (backend / Android / docs / build-hardening
  commits).
- ⚠️ CI workflow files remain at `ci/workflows/` — pushing real GitHub workflows
  needs the `workflows` permission; activation is one command (`ci/README.md`).

## 5. Risks update

- **SMS cost/abuse** (still the #1 risk): unchanged posture — sandbox `log` provider
  now, rate limiting + resend cooldown + 5-attempt cap already enforceable; provider
  selection (Twilio Verify / Firebase / Unifonic) is Phase 06 with budget guardrails.
- **Legal review** (§37/§L): skill `requiresCertification` flags, trade-licence checks
  and the later paid-assessment workflow are **flagged for professional UAE legal
  review** — Phase 03 implements no legal claims beyond the single unambiguous
  driver-licence flag.
- **Offline session restore**: when the device can't reach the API at cold start, a
  stored session degrades to sign-in (tokens are kept for the next launch). Honest,
  safe, documented; offline-first caching is not a V1 goal.
- **Shell/DI page resolution** and absolute-route navigation are standard MAUI
  patterns but unverifiable here — first-boot smoke test explicitly walks them
  (guide §3). If navigation misbehaves on the first real build, that's the first
  place to look.

## 6. Next phase (proposal — per §62, waiting for your instruction)

**Phase 04 — Jobs core:** employer vacancy posting (title, skills from catalog, pay
range, location, work type) → worker feed filtered by emirate/skill/availability
→ vacancy details → apply/withdraw with server-confirmed states; admin-lite continues
in background. (Name/payment/assessment phases follow as per the master roadmap.)

## ⛔ CHECKPOINT

Phase 03 is complete and **ready for first boot** — exact steps in
[first-boot-guide.md](first-boot-guide.md). Nested STOP applies (§62):
no Phase 04 work starts until you say so.
