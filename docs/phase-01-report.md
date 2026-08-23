# KaamLink (DIRECTHIRE UAE) — Phase 01 Report
**Project discovery · product/technical architecture · Android-first foundation**

- Date: 2026-08-23 · Branch: `arena/01a02fc9-kaamlink`
- Scope of this phase: audit what existed, choose/confirm the architecture, and establish a clean, honest foundation. **No product features were built in this phase.**
- Environment caveat (verification honesty): this sandbox has **no .NET SDK** and **no network egress to NuGet/workload sources** (confirmed: TLS failure to `dot.net` and `api.nuget.org`). Deleted/rewritten code was therefore **statically verified** (XML well-formedness, cross-reference checks) but **not compile-verified**. First action on a developer machine or CI must be `dotnet restore && dotnet build` (commands in §Q).

---

## A. Project status

**What existed before Phase 01 (91 files, single commit `1653839`):**

The repository contained **no product code**. It was an unmodified mix of two Microsoft defaults:

1. A `dotnet new maui` template shell (Hello-World `MainPage` with counter button, default purple .NET branding, `com.companyname.kaamlink` application ID).
2. A copied-in **.NET MAUI workshop sample app** (a personal task/project tracker: `Project`, `ProjectTask`, `Category`, `Tag`, seed JSON, Syncfusion charts) in `Data/`, `Models/`, `PageModels/`, `Pages/`, `Services/`, `Utilities/`.

That sample/state had zero relevance to a hiring platform and was **broken as committed**:

| # | Defect found in audit | Evidence |
|---|---|---|
| 1 | **Did not compile as committed** — code referenced packages absent from the csproj | `Microsoft.Data.Sqlite` (all of `Data/`), `CommunityToolkit.Mvvm` (`PageModels/`), `CommunityToolkit.Maui` (`Models/Tag.cs`), `Syncfusion.Maui.Toolkit/Charts` (`Pages/`) — csproj referenced only `Microsoft.Maui.Controls` + `Logging.Debug` |
| 2 | **Case-sensitivity build break on Linux/macOS/CI** | `KaamLink.slnx` → `Path="Kaamlink.csproj"` but file on disk is `KaamLink.csproj` |
| 3 | **Two different apps in one project** | Root `Kaamlink.MainPage` (Hello World) was the only routed page (`AppShell`); sample pages (`KaamLink.Pages.*`) were orphaned — no DI registration anywhere (`MauiProgram` registered nothing) |
| 4 | **Inconsistent namespaces** | `Kaamlink` vs `KaamLink.*` across files |
| 5 | **Fake/demo data as shipped content** | `Resources/Raw/SeedData.json` demo projects ("Balance", "Survey Employees") |
| 6 | **Code smells** | `async void ClearTables()`, swallowed exceptions (`Console.WriteLine(e)`), typo `TaskRespository.cs`, duplicated template files |
| 7 | **No product infrastructure** | No backend, no auth, no API layer, no real local DB schema, no tests, no CI/CD, no docs (README = `# Kaamlink`), no secrets hygiene in `.gitignore` |
| 8 | **Branding/identity defaults** | .NET robot icon/splash, purple `#512BD4` theme, `com.companyname.*` ID |

**Disposition:** per instruction, all sample code was deleted and the app project rewritten as a clean foundation (details §Q). Nothing worth keeping was lost (git history retains the sample for reference).

**What exists after Phase 01:** a minimal, coherent, Android-only MAUI foundation (role-separated entry, Shell navigation skeleton, DI composition root, design tokens, EN+AR localization, hardened manifest/gitignore, docs + ADRs). No backend, database, or features exist yet — by design.

---

## B. Technology stack

### Current (after this phase)

**Android (all there is today):**
- Framework: **.NET MAUI** (`net10.0-android` only), C# / XAML, .NET 10 SDK, XAML source-gen inflation
- Min Android **API 21 (5.0)** · target SDK = latest .NET 10 Android target (Play-compliant)
- UI: XAML + implicit/explicit styles · Navigation: **Shell** (routes centralized in `AppRoutes`) · State: **MVVM** via CommunityToolkit.Mvvm source generators
- DI: `Microsoft.Extensions.DependencyInjection` (MAUI built-in) · Logging: `Microsoft.Extensions.Logging.Debug` (DEBUG only)
- Networking / local DB / secure storage / push / analytics / crash reporting / tests: **none yet** (recommendations below — nothing is coupled to a provider today)

**Backend / database / storage / auth / deployment / CI:** did not exist → zero upward-compat constraints. Greenfield.

### Recommended target stack (decisions: ADR 0001–0003)

| Area | Recommendation | Why (short) |
|---|---|---|
| Client | **Keep .NET MAUI, Android-only V1** | Meets all requirements; no product code wasted; C# end-to-end (ADR 0001) |
| Backend | **ASP.NET Core 10, modular monolith**, versioned REST + OpenAPI | One language, boring & proven, web-portal ready (ADR 0002) |
| Database | **PostgreSQL 16 + EF Core 10** (code-first migrations) | Relational core fits hiring domain; constraints/indexes/txn integrity |
| Object storage | **S3-compatible** (Cloudflare R2 or Backblaze B2), presigned up/download | Cheap, provider-swappable behind `IFileStorage` |
| Auth | Phone-number OTP (SMS) → **short-lived JWT access + refresh token rotation**; secure token storage via MAUI SecureStorage (Keystore) | Practical for the worker demographic; passwords optional later for employers |
| Push | **Firebase Cloud Messaging** (free) behind `INotificationService` | Standard for Android |
| Client local DB | **SQLite** via EF Core InMemory? No — **sqlite-net-pcl** (or EF Core SQLite) for offline cache/sync queue, added in the offline phase | Mature on MAUI; only added when §H is implemented |
| Crash/analytics | Sentry (free tier) or App Center successor; decide at release phase | Avoid premature spend |
| CI/CD | **GitHub Actions** (build Android, run tests, lint secrets) | Free for this repo shape |
| Hosting | Phase 1: PaaS free tier (Render/Fly.io + Neon/Supabase Postgres free) **or** one ~€5–15 VPS (Hetzner) + R2 free tier | Near-zero cost; documented upgrade path (§J) |

Package discipline on the client: currently exactly **3** package references (MAUI itself, debug logging, MVVM toolkit). Additions (SQLite, FCM, HTTP resilience `Microsoft.Extensions.Http.Resilience`, image caching) arrive only with the feature that needs them.

---

## C. Architecture

### Current (post-cleanup) client architecture
Single MAUI app → Shell → feature folders (`Features/Onboarding|Worker|Employer`) with page+viewmodel pairs, DI composition root (`MauiProgram.RegisterServices`), cross-cutting concerns in `Core/`. Deliberately thin — it is a skeleton, not an architecture claim.

### Problems (before) → resolved
The "architecture" before was two unrelated sample apps stapled together, non-compiling, with mixed namespaces and no DI — fully removed (§A, §Q).

### Recommended overall architecture
```
Worker Android ─┐
Employer Android┤  (same V1 app; separate experience roots/permissions)
Web portals ────┤  (future; same API, zero backend changes)
                ▼
        API gateway/host (ASP.NET Core, modular monolith)
          Modules: Identity · Profiles · Companies · Jobs · Applications
                   SiteVisits · Assessments · Documents · Messaging · Admin
                ▼
   Application/services → Repositories → PostgreSQL | Object storage | FCM
```
Principles: backend is UI-agnostic (no Android-coupled contracts); business rules live server-side only; modules isolated by internal interfaces so extraction is possible later, but **one deployable** (no premature microservices).

---

## D. Android architecture

**One codebase, two clearly separated experiences** (per §5/§6 of the brief — no duplicated apps):

```
KaamLink (app)
 ├─ Core/            navigation registry, future: config, error handling, sync, connectivity
 ├─ Features/
 │   ├─ Onboarding/  role selection (today) → phone OTP + progressive setup (Phase 02)
 │   ├─ Worker/      worker root → Profile/SkillPassport/Jobs/Applications/…
 │   ├─ Employer/    employer root → Company/Vacancies/Candidates/Assessments/…
 │   └─ Shared/      auth screens, documents viewer, messaging UI (later)
 ├─ Resources/Styles   one design system (tokens + styles)
 ├─ Resources/Strings  EN neutral + AR satellite; RTL-native
 └─ Platforms/Android  thin platform layer only
```
- **Separation mechanism:** separate Shell navigation roots + separate viewmodels/features per experience; at auth time the server tells the app which experience root to show (role-gated navigation replaces today's simple push — flagged in code comments).
- **Later split into Worker/Employer Play apps (if commercially useful):** the layout above splits by moving `Features/Worker` / `Features/Employer` behind two thin app heads sharing `Core` + API — backend unchanged.
- **Conventions:** MVVM everywhere; viewmodels small; compiled bindings (`x:DataType`); strings via `AppStrings`; routes via `AppRoutes`; colors via tokens; ViewModels never touch HTTP/SQLite directly (services do).

---

## E. Backend architecture (recommended, build in Phase 02+)

- **API:** versioned REST (`/api/v1/...`), OpenAPI published; problem-details error model; **server-side pagination/filtering contracts mandatory before any Android list exists**.
- **Application layer:** use-case services per module (e.g. `ApplicationService.AdvanceAsync(applicationId, toStatus)`), owning validation + transactions. Controllers stay dumb.
- **Repositories/data:** EF Core; per-tenant/caller query filters (worker sees own data; employer sees own company scope — enforced in the data layer, not by convention).
- **AuthN:** phone OTP → JWT access (~15 min) + rotating refresh tokens; token reuse detection.
- **AuthZ:** policy-based. Worker endpoints: owner-only. Employer endpoints: `CompanyMember` role check (OWNER/ADMIN/HIRING_MANAGER manage; SUPERVISOR conduct assessments; VIEWER read-only). Platform admin: separate scope. **All enforcement server-side.**
- **Storage:** `IFileStorage` → S3-compatible; upload = validated presigned PUT (type/size/AV-scan hook); download = short-lived presigned GET behind authorization; never public buckets (§I).
- **Sync:** offline clients push idempotent commands (`Idempotency-Key` + client-generated entity IDs); server returns canonical state; conflicts resolved server-authoritative for shared entities (§H).
- **Notifications:** `INotificationService` → FCM; events emitted by application services (application status changed, site-visit scheduled, etc.).

---

## F. Database (recommended V1 model)

V1 entities (relational; PK = surrogate `uuid`/bigserial; `created_at`/`updated_at` everywhere; soft-delete only where legally safe):

**Identity & membership:** `users` (phone [unique], status, locale) · `worker_profiles` (user_id FK uniq, name, photo_url, profession, headline, summary, expected_salary_min/max, availability, location_id, profile_completeness) · `companies` (name, trade_license_no [unique], category, location_id, verification_state, description) · `company_members` (user_id, company_id, role; uniq(user,company))
**Skills:** `skills` (name, category_id, requires_certification flag) · `skill_categories` · `worker_skills` (worker_id, skill_id, years_experience, verification_state: SELF_DECLARED/EMPLOYER_VERIFIED/ASSESSMENT_VERIFIED, source_ref; uniq(worker,skill)) · `worker_experiences` (worker_id, title, employer_name, from/to, description)
**Documents:** `worker_documents` (worker_id, type, object_key, status: UPLOADING/PENDING_REVIEW/VERIFIED/REJECTED, expires_at) — keys, never public URLs
**Jobs:** `jobs` (company_id FK, title, occupation/skill refs, description, **workers_required int ≥ 1**, salary_min/max, allowances/accommodation/transport/food flags or amounts, shift, hours, location_id, start_date, assessment_required bool, requirements_text, status: DRAFT/OPEN/PAUSED/CLOSED/FILLED, closes_at, published_at) · `job_required_skills`/`job_preferred_skills` (joins → enables §20 multi-hire + §30 matching without redesign)
**Pipeline:** `applications` (job_id, worker_id, status, uniq(job,worker), applied_at, status_changed_at) · `application_status_history` (application_id, from, to, actor_id, at, note) — **transitions only via the centralized state machine below** · `site_visits` (application_id, scheduled_at, location, instructions, contact, status, attendance) · `assessment_templates` (occupation, name) + `assessment_criteria` (template_id, name, scale, weight, sort) — **configurable per profession, never hardcoded** · `assessments` (site_visit_id/application_id, template_id, status, attendance, compensation jsonb: {agreed_amount, currency, payment_status: AGREED/CONFIRMED_PAID/DISPUTED, reference, worker_ack, employer_ack}, notes) · `assessment_scores` (assessment_id, criterion_id, score) · `offers` (application_id, salary, terms, status, expires_at) · `joining_records` (offer_id, start_date, status, worker_confirmed_at, employer_confirmed_at)
**Ops:** `notifications`, `audit_log` (actor, action, entity_type/id, at, metadata-jsonb — never secrets), `reports` (trust & safety, later).

**Key indexes:** `applications(job_id,status)`, `applications(worker_id)`, `jobs(status,location_id)`, `worker_skills(skill_id)`, `company_members(company_id,role)`, `audit_log(entity_type,entity_id)`, FK columns.
**Migration strategy:** EF Core code-first migrations from the first migration; each release applies migrations in CI/CD deploy step; rollback = reverse migration or restore point; seed reference data (skills/occupations) via idempotent seeders.

**V1 application status machine** (server-enforced; invalid transitions = 409):
`APPLIED → VIEWED → SHORTLISTED → SITE_VISIT_INVITED → SITE_VISIT_CONFIRMED → ASSESSMENT_SCHEDULED → ASSESSMENT_COMPLETED → DECISION_PENDING → SELECTED → OFFER → JOINING → JOINED`, with `REJECTED`, `HOLD`, `WITHDRAWN` (worker) as terminal/exit states from defined points. History table captures every transition with actor + timestamp (audit §47).

---

## G. Authentication & onboarding

- **Model (ADR 0003):** `User` = auth identity only. `WorkerProfile` 1:0..1 per user (survives employers). `Company` + `CompanyMember(role)`. One human may be both worker and company member; role-selection picks the *experience*, not an account "type".
- **Worker onboarding (progressive, Android-optimized):** phone OTP → name+photo → profession → skills (+years) → location/availability → expected salary → (later, optional) documents. Every step optional except phone+name; education **not mandatory by default**; profile-completeness indicator encourages completion. Certifications/licences attachable to skills that legitimately require them.
- **Employer onboarding:** phone OTP → contact person → company profile (name, category, location) → trade-licence + verification submission → `PENDING`. Capabilities until `VERIFIED`: full job drafting, but **publication gated** (interim rule — final gating is a policy decision → also flagged §L). No company is ever displayed as verified without the verification record.
- **Authorization matrix:** worker=owner of own profile/applications; company roles as in §E; platform admin separate; UI hides, server blocks.

---

## H. Offline architecture

Factory/site context ⇒ **cloud is source of truth; device is a cache + queue**.

- **Local store:** SQLite (cache of jobs/applications/assessment forms + drafts + sync queue). Encrypted at rest by OS; no secrets/PII beyond need.
- **Write path:** offline actions (e.g. record assessment scores, attendance) → enqueue command row `{idempotent_key, entity, payload, state: PENDING|SYNCING|FAILED|DONE, attempts}` → immediate local UI reflect → background sync (WorkManager-style via MAUI background tasks) with exponential backoff; **server dedupes by idempotency key** (no double-assessments, no double-applications).
- **Read path:** cache-first for lists with TTL + server deltas (`updated_since`); placeholder skeletons on slow networks; images resized + thumbnail-cached.
- **Conflicts:** server-authoritative for pipeline status (employer decision wins; client re-renders + notifies); last-writer-wins with audit only for own drafts.
- **Cleanup:** synced temp media/assessment artifacts purged after server confirmation; queue bounded; stale-draft policy.
- **Never local-only:** any hiring record (decision, score, payment arrangement, offer) is provisional until the server confirms — the UI labels it "pending sync", never as final (§50 no-fake rule).

---

## I. Security

**Found (before):** no auth model, no secure storage, `allowBackup=true`, debug logging always-on capable, no `.gitignore` secret rules, no secrets present (good). **Fixed this phase:** `allowBackup="false"`, secret/keystore ignore rules, DEBUG-gated logging, minimal permissions, no hardcoded anything.

**Recommended model:**
- Tokens in **MAUI SecureStorage** (Android Keystore-backed), never plain SharedPreferences/files; refresh-token rotation + reuse detection server-side; device-level screen-lock recommended.
- API: TLS everywhere; cert-pinning evaluated at release (ops cost vs. benefit); rate limits on OTP, login, search; per-endpoint authorization (§E/G); request size limits; input validation centrally.
- Documents: private bucket; presigned URLs (upload ≤ ~10MB, mimetype+magic-byte validation; download short-lived, authorized); visibility tiers **PUBLIC profile / CONTROLLED (shared during active application/documents with consent) / PRIVATE (identity docs) / ADMIN-ONLY**; deletion + retention policy (account deletion erases PII + docs, keeping only legally-required audit trails).
- Audit log: actor/action/entity/at/metadata — **never** passwords, OTPs, tokens.
- Secrets: env/secret-manager per environment (dev/stage/prod); nothing in repo/APK (`.gitignore` now enforces; CI adds a secret-scan step).
- Abuse prevention: report/block entities already modeled (`reports`), moderation queue later; no public negative ratings.
- OWASP MASVS as the mobile checklist for release gate.

---

## J. Cost (realistic, low-cost start → upgrade path)

| Stage | Infra | Est. monthly |
|---|---|---|
| Dev/alpha (10–100 users) | GitHub free CI · Render/Fly free web service · Neon/Supabase Postgres free · Cloudflare R2 free · FCM free · SMS via Msg91/Twilio verify trial | **~0–10 USD** (+SMS) |
| Beta (500–1,000) | One Hetzner CX22–32 (~€5–15) running API+worker; managed Postgres small (~$15) or self-host; R2 (~$1–3); Sentry free | **~10–30 USD** |
| Growth (5,000) | API on 2×CX32 + managed Postgres (~$40–70), R2 + CDN, backups, uptime | **~60–120 USD** |

Upgrade path (no rewrites): move Postgres→managed HA, API→multi-instance behind LB, storage→S3+CloudFront/Cloudflare CDN, add Redis cache, read replicas — each a deploy-config change because clients only see REST/presigned URLs. **Not** optimized only for free tier: schema, indexes, pagination, and module boundaries are production-grade from day one.

---

## K. Google Play foundation

- Target latest SDK (net10-android target satisfies current Play targetSdk policy; re-verify at release — policy updates annually, required Aug of each year).
- **Permissions: `INTERNET`, `ACCESS_NETWORK_STATE` only today.** Camera (document capture) and notifications (`POST_NOTIFICATIONS`, API 33+) arrive with their features, requested just-in-time with rationale UI. Never SMS/contacts/mic/background-location.
- Account deletion: **in-app delete + web parity required by Play** — designed into auth module (§I).
- Data safety form, privacy policy, UGC moderation & reporting (worker/employer/job reports — modeled), app signing by Play (upload keystore secured offline, ignored by git), release/AAB build, crash reporting. Don't claim compliance until the release checklist is run.

---

## L. UAE compliance — for professional review (not legal advice; nothing here asserts law)

1. **Paid practical training/assessment**: whether trial-day work requires a permit/contract, wage treatment, and what the platform may record/facilitate (we store an *agreed arrangement + acknowledgements*, never claim payment occurred without confirmation — but the legal framing needs UAE counsel). Review against MOHRE rules.
2. **Recruitment activity**: whether operating a hiring marketplace constitutes recruitment/mediation requiring a MOHRE licence or specific terms.
3. **Employment relationships**: joining/offer flows must not imply a legal employment relationship before real-world steps (work permit, contract, visa) complete.
4. **Personal data**: UAE PDPL (Federal Decree-Law 45/2021) — consent, purpose limitation, cross-border storage (initial hosting may be outside UAE), retention/deletion, identity-document handling (Emirates ID/passport).
5. **Wage/compensation records**: any payment-recorded feature vs. WPS obligations (platform must not position itself as payroll).
6. **Discrimination language** in job posts/matching (we exclude protected attributes from ranking — verify alignment with UAE anti-discrimination law).
7. **Arabic language requirements** for certain official touchpoints.

---

## M. UI/UX direction

- One design system: tokens (`Resources/Styles/Colors.xaml`) + styles (`Styles.xaml`) — teal-based trust palette (replaced .NET purple), Open Sans, 48dp+ touch targets, 14sp+ body, restrained cards, no gradients/glassmorphism.
- **Worker UX:** visual, minimal text, progressive profile, job cards answering *what pays, where, what skills, how many vacancies*; actions View/Apply/Save.
- **Employer UX:** pipeline-first dashboard (open vacancies → applicants → visits → assessments → decisions → offers) with counts and next actions; match cards explain *why* (skills x/y, years, location, availability) — explainability is a UI requirement, not a marketing line.
- Accessibility: semantic headings/descriptions (already in skeleton), contrast-checked palette, scalable text; RTL: `supportsRtl=true`, mirrored layouts verified in Arabic, Arabic from day one (skeleton already ships ar strings); Hindi/Urdu later via same mechanism.
- Performance budget on low-end: cold start < 2.5s (profiled AOT), lists virtualized + paged, images thumbnail-cached.

---

## N. Testing

**Found (before):** zero tests, zero CI. **Now:** still zero tests — honest state; the architecture now permits them (DI + small VMs).

**Recommended test architecture:**
1. **Unit** (xunit): viewmodels, validation, status-machine transitions, matching explainability, sync-queue reducer. (Executable cross-platform — start Phase 02.)
2. **Backend integration**: WebApplicationFactory + Testcontainers-Postgres → API end-to-end (auth, authorization matrix, transitions, idempotency).
3. **DB tests**: migration up/down, constraints, indexes on hot queries.
4. **Android UI**: MAUI UITest/Appium smoke for golden path (role select → login → apply); device-farm run on 1–2 low-end devices.
5. **Contract tests**: OpenAPI snapshot vs. client DTOs (shared contract project).
CI gates: build (android) + unit + API tests on PR; signing only on release branches.

---

## O. Risks (ranked)

**CRITICAL**
- **R-1 Compliance-legality of the paid-assessment workflow** (§L-1/2/3): building it before legal review could facilitate unlawful practice. *Mitigation: feature-flagged, built last in V1 core, legal sign-off gate.*
- **R-2 Verification integrity**: falsely "verified" companies/skills destroys trust and may create liability. *Mitigation: server-side verification records only; UI binds strictly to them.*

**HIGH**
- **R-3 No backend exists** — everything server-side ($E–$J) is greenfield; schedule risk for auth+OTP+storage correctness. *Mitigation: Phase 02 is backend-first; thin-slice the golden path early.*
- **R-4 Offline sync correctness** (duplicates/lost assessments on site). *Mitigation: idempotency keys, queue FSM tests, server-authoritative conflicts.*
- **R-5 Authorization leakage** (employer seeing other company's data / worker PII exposure). *Mitigation: data-layer filters, authorization-matrix integration tests, document ACLs.*
- **R-6 OTP SMS cost/deliverability in UAE** at scale. *Mitigation: provider abstraction, rate-limit + abuse controls, fallback channels later.*

**MEDIUM**
- **R-7 Build not yet compile-verified in this sandbox** (no SDK/egress). *Mitigation: first CI run = gate; static cross-checks already done (§Q).*
- **R-8 MAUI low-end performance** (startup/RAM). *Mitigation: profiled AOT, apksize/startup budget check in beta gate; Kotlin pivot remains a documented fallback (ADR 0001).*
- **R-9 Data-model churn** if legal forces different assessment/payment semantics. *Mitigation: compensation stored as flexible sub-record; migrations from day one.*
- **R-10 Scope creep** (features beyond core loop §57). *Mitigation: roadmap gates; "do not build prematurely" list (§58) enforced in review.*

**LOW**
- **R-11 BaaS/vendor lock-in** — mitigated by provider interfaces chosen (§35).
- **R-12 Single-person knowledge concentration** — mitigated by ADRs + this report.

---

## P. Implementation roadmap (dependencies respected; no skipping)

**Phase 02 — Backend skeleton + auth.** *Goal:* deployable API with identity, OTP auth, JWT, authorization framework, Postgres + migrations, audit log, environments (dev/stage/prod), CI. *Deps:* none. *Deliverables:* ASP.NET Core solution (modular monolith), `users`/auth tables, OTP provider interface + sandbox impl, rate limiting, OpenAPI, GitHub Actions (build+test), Terraform-free runbook docs. *Testing:* auth/OTP unit + integration tests, authorization matrix tests. *DoD:* `register→verify→token→authorized request` proves end-to-end in staging; CI green; secrets via env.

**Phase 03 — Profiles & companies.** Worker onboarding API+app screens (progressive), skills reference data, company onboarding + verification states, admin-lite verification endpoint (ops via API/Postman until admin UI), company membership/roles. *DoD:* worker & employer complete onboarding from clean installs in EN and AR; server enforces role scopes; migrations reproducible.

**Phase 04 — Jobs & applications.** Vacancy CRUD (multi-worker), publication gating, worker job discovery (paged, filtered, explainable cards), apply/save, application pipeline with state machine + history, employer applicant views. *DoD:* full worker publish→apply→shortlist round-trip slow-network-tested; transitions reject invalid moves (409s); pagination verified at 10k-job seed.

**Phase 05 — Site visits + assessment engine.** SiteVisit scheduling/confirm/decline/attendance (FCM notifications), configurable AssessmentTemplate/Criteria + assessment capture, offline capture + sync queue (idempotent), compensation arrangement record (feature-flagged pending §L sign-off), decision states. *DoD:* airplane-mode assessment → reconnect → exactly-once server record; offline UI never shows unconfirmed data as final.

**Phase 06 — Offers, joining, history, trust.** Offers, worker acceptance, joining confirmations, employment history, reports/block, document upload v1 (presigned, validated), privacy-policy parity + account deletion. *DoD:* golden path worker register→…→joined passes on a low-end device in EN+AR; security checklist (§I) executed; Play data-safety prepped.

**Phase 07 — Beta hardening/launch.** AOT/size/startup budgets verified vs §M, crash reporting, analytics (opt-in), backups/restore drill, monitoring, Play internal→closed testing, launch checklist. *DoD:* R-1..R-6 mitigations evidenced; legal sign-offs attached; store listing complete.

(Admin web portal, billing, AI matching, WhatsApp etc. = post-V1 per §58/§59.)

---

## Q. File/project changes made in this phase

**Deleted (sample/template code — reason: no product value; blocked compile; user directed complete rewrite; risk: none, history retained; test: static verification of final tree):**
- `MainPage.xaml(.cs)` (Hello-World), `Data/*` (6 sample repositories/seed), `Models/*` (7 sample models), `PageModels/*` (5), `Pages/**/*` (23, incl. Syncfusion sample controls), `Services/*` (2), `Utilities/*` (2), `Resources/Raw/SeedData.json` + `AboutAssets.txt` (fake data), `Resources/Images/dotnet_bot.png`, sample fonts (`FluentSystemIcons-Regular.ttf`, `FluentUI.cs`, `SegoeUI-Semibold.ttf`), `Resources/Styles/AppStyles.xaml` (duplicate style file), `Platforms/{iOS,MacCatalyst,Windows}/*` (V1 is Android-only; restorable from history), `Properties/launchSettings.json` (Windows-only debug profile).

**Rewritten:**
| File | Change | Reason | Risk | Test performed |
|---|---|---|---|---|
| `KaamLink.csproj` | Android-only TFM; RootNamespace `KaamLink`; real app metadata; added `CommunityToolkit.Mvvm`; removed sample refs; brand colors for icon/splash | Compile-blocking sample deps removed; V1 scope; DI-able MVVM standard | Restore needs network (normal) | XML well-formedness + reference audit |
| `KaamLink.slnx` | `Kaamlink.csproj`→`KaamLink.csproj` | Case-sensitivity build break (Linux/CI) | — | Case match verified |
| `GlobalUsings.cs` | Toolkit + Core.Navigation only | Remove sample usings | — | Namespaces exist |
| `MauiProgram.cs` | Real composition root (`RegisterServices`), DEBUG-gated logging, fonts | No DI existed before | — | Registrations match all page/VM types |
| `App.xaml(.cs)`, `AppShell.xaml(.cs)` | KaamLink namespace; Shell hosts role-selection; route registration + comments | Separate experience entry per §5 | — | Routes match `AppRoutes` constants |
| `Platforms/Android/AndroidManifest.xml` | `allowBackup="false"`, min permissions, RTL, comments | §31/§38/§40 security+Play | None (backup UX revisited later) | XML valid; permission audit |
| `Platforms/Android/Resources/values/colors.xml` | Brand palette | Remove .NET purple | — | Token match |
| `Platforms/Android/MainActivity.cs`, `MainApplication.cs` | Namespace `Kaamlink`→`KaamLink` | Namespace consistency (#4) | — | Matches csproj RootNamespace |
| `.gitignore` | +keystore/secrets/env/IDE rules | §38/§54 | None | Pattern sanity |
| `README.md` | Product README (status, stack, build, rules) | Was `# Kaamlink` | — | — |

**Added:**
| File | Purpose |
|---|---|
| `Core/Navigation/AppRoutes.cs` | Central route registry (no magic strings) |
| `Features/Onboarding/RoleSelection*` | 👷/🏭 role entry (localized, accessible) |
| `Features/Worker/WorkerHome*` | Worker experience root (honest placeholder) |
| `Features/Employer/EmployerHome*` | Employer experience root (honest placeholder) |
| `Resources/Styles/Colors.xaml` | Design tokens (brand/semantic/neutrals) |
| `Resources/Styles/Styles.xaml` | Design system (typography, card, primary button) |
| `Resources/Strings/AppStrings.resx` / `.ar.resx` / `.Designer.cs` | EN+AR strings + committed strongly-typed accessor (CLI-build-safe) |
| `Resources/AppIcon/appicon*.svg`, `Resources/Splash/splash.svg` | Interim "K" brand mark (replace in branding phase) |
| `docs/adr/0001..0003` | Stack / backend / identity-model decisions |
| `docs/phase-01-report.md` | This report |

**Environment/material that could not be inspected or executed:** live .NET build (no SDK), NuGet restore (egress blocked), device/emulator run. **First verification command sequence for a dev machine/CI:**
```bash
dotnet workload install maui-android
dotnet restore KaamLink.csproj
dotnet build KaamLink.csproj -f net10.0-android -c Debug   # must be green; gate for Phase 02
```
Cross-checks done statically in lieu of compile: well-formed XML on all XAML/resx/manifests/csproj; every `x:Static strings:AppStrings.*` key exists in both resx files and has a Designer property; every `{Binding}` command matches toolkit-generated command names; every `Style` key referenced exists in `Styles.xaml`; every route constant used matches Shell registration.

---

## Phase 01 checkpoint — STOP

Foundation delivered as specified; full product development has **not** begun (per §62 of Master Prompt 01). Awaiting the next instruction (natural next step: **Phase 02 — backend skeleton + authentication**).
