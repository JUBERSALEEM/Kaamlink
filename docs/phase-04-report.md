# KaamLink — Phase 04 Report: Jobs core (vacancy posting → worker feed → apply)

**Status:** implementation complete, static verification green (101 phase-03 +
101 phase-04 checks, 0 failures). **Pending:** your first local compile is the
formal gate — the sandbox has no .NET SDK, exactly like Phase 03 (which your
machine then proved in 3 short compile rounds).

---

## 1. Scope delivered (Definition of Done)

| # | Phase-04 requirement | Delivered |
|---|---|---|
| 1 | Employer posts vacancies | `POST /api/v1/companies/{id}/vacancies` + edit, Draft-first lifecycle |
| 2 | Publishing is gated | `→ Active` requires a **Verified** company (`jobs.company_not_verified`) |
| 3 | Worker feed | `GET /api/v1/vacancies` — Active only, emirate/skillId/keyword filters, take+1 paging (≤50) |
| 4 | Apply | `POST /api/v1/vacancies/{id}/applications` — onboarding gate + one-per-vacancy (DB-unique) |
| 5 | Employer sees applicants | list + Viewed/Shortlisted/Rejected/Hired transitions, terminal finality |
| 6 | Worker tracks applications | my-applications list + withdraw while decision is open |
| 7 | Android client | 6 real pages wired into both homes (no placeholders, §50) |
| 8 | Audit trail | 6 new `jobs.*` audit actions, no sensitive payloads |
| 9 | Tests | **24 new**: 19 unit + 5 end-to-end integration flows |
| 10 | Data-layer | hand-written migration #3 `20260824120000_Phase04Jobs` (+Designer, +ModelSnapshot) |

## 2. Business rules (product decisions — documented, NOT legal claims)

- **Role matrix:** vacancy/application writes → `Owner/Admin/HiringManager`;
  reads (company vacancy list) → any member. Applicant lists contain **phone
  numbers** (the direct-hiring point) and are hiring-role only.
- **Verification gate:** only `Verified` companies can publish (→ Active).
  Drafts are allowed while Pending so employers aren't blocked onboarding.
- **Apply gate:** requires the server's own `OnboardingComplete` (Phase 03
  definition — clients never invent their own).
- **Terminal finality:** Rejected/Hired/Withdrawn never change again. **V1
  decision:** `Withdrawn` is also terminal — no re-apply to the same vacancy
  (avoids employer-list churn; revisit with product feedback).
- **Non-leak scoping:** foreign-company access is 404 (never 403), same as
  missing records.
- **Validation:** title 2–120, description ≤4000, salary pair-or-neither,
  0–1,000,000 AED, headcount 1–500, 1–10 catalog skills, cover note ≤500.

## 3. Architecture

- **Domain:** `Vacancy` (status machine `Draft/Active/Paused/Closed`, guarded
  transitions via `CanTransitionTo`), `VacancySkill` (composite key), `JobApplication`
  (`Applied/Viewed/Shortlisted/Rejected/Hired/Withdrawn`). All invariants in the
  domain; services only orchestrate.
- **Application:** `VacancyService`, `JobApplicationService` + ports
  `IVacancyRepository`, `IJobApplicationRepository` (same port/adapter shape as
  Phase 03). Response dressing (company names, skill names) happens in services
  via repositories — EF navgations are used for FK integrity only, so the
  services stay trivially unit-testable with in-memory fakes.
- **Persistence:** one DbSet per entity, FK rules (company→cascade,
  creator→restrict, application links→cascade), feed index
  `(status, created_at)`, **unique `(vacancy_id, worker_profile_id)`** —
  dedupe is a database guarantee, not just a code check.
- **Api:** three controllers (`VacanciesController` worker, `WorkerApplicationsController`,
  `CompanyVacanciesController` employer). All `[Authorize]`; rate-limit policies
  from Phase 02 still guard auth endpoints; feed is behind auth in V1.
- **Client:** `Features/Jobs/` — feed (filters + paging), detail+apply,
  my-applications (withdraw), employer vacancy list with lifecycle buttons,
  post/edit form (catalog multi-select), applicants with decision buttons.
  58 new strings × EN/AR; status pills via one shared `StatusColorConverter`;
  source-generated JSON only (trim-safe).

## 4. Verification

- `tools/verify-phase03.py` → **101 checks, 0 failures** (incl. automatic
  EN/AR/designer parity on all 167 keys and route-drift across the app).
- `tools/verify-phase04.py` (new) → **101 checks, 0 failures**: migration ↔
  snapshot ↔ designer coherence, client↔server route parity, enum mirrors,
  DI/serializer registrations.
- Unit tests: `VacancyServiceTests` 10 + `JobApplicationServiceTests` 9.
- Integration (`JobsFlowTests`, Testcontainers PostgreSQL): publish gate →
  feed visibility; apply → dedupe → own-status → shortlist → worker sees state;
  onboarding gate (fresh worker rejected); anonymous 401 + foreign-company 404;
  withdraw → terminal both sides.
- **Migration self-check on your machine (recommended):**
  `dotnet ef migrations has-pending-model-changes -p backend/src/KaamLink.Infrastructure/KaamLink.Infrastructure.csproj -s backend/src/KaamLink.Api/KaamLink.Api.csproj`
  → must print "No model changes" (validates the hand-written snapshot against
  the real model).

## 5. Known limitations (honest list)

1. **Edit re-asks required skills:** the employer summary endpoint doesn't
   return a vacancy's skills (worker detail does, but only for Active). The form
   blocks saving without a fresh selection — server enforces 1–10 anyway.
2. **Paging is take+1** (`HasMore`, no total count query).
3. Feed **skill filter** exists in the API (`skillId=`) but the V1 UI exposes
   emirate + keyword only.
4. No N+1 batching in response dressing (per-vacancy company/skill lookups;
   fine up to 50-row pages; revisit when profiling says so).
5. No re-apply after withdrawal (V1 terminal rule, above).

## 6. Run the demo (requests.http has the full Phase 04 section)

1. `docker compose -f backend/docker-compose.yml up --build` → API on :8080,
   migration #3 applies on startup.
2. As employer: register company → **admin approves**
   (`POST /api/v1/admin/companies/{id}/review` with the bootstrap admin phone)
   → post vacancy → publish.
3. As worker (app or requests): complete onboarding → feed shows the vacancy →
   apply → withdraw or let the employer shortlist/hire.
4. App paths: Worker home → *Browse jobs* / *My applications*; Employer home →
   *Vacancies* on each company card.

## 7. Test totals after Phase 04

- **74 total = 54 unit + 20 integration** (Phase 02/03: 35 + 15; Phase 04: +19 + 5).

## 8. Next (Phase 05 candidates, per Master Prompt roadmap)

Skill verification/assessments, employer visit scheduling, vacancy expiry &
refresh, saved searches, notification spine (push), company media/verification
documents — **not started; awaiting your "continue phase 5" after the checkpoint
review + first local build.**
