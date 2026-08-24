# Phase 05 Report — Skill Assessments (the trust spine)

Status: **code complete, static checks green (88/88 + 105/105 + 103/103 verifiers); the user's machine is the formal build/test gate.**

## Why this phase

The Master Prompt positions KaamLink for workers where **education is not mandatory** — so trust cannot come from degrees. It must come from *proof*: a worker claims a skill (SelfDeclared), and the platform gives them a way to prove it. Phase 05 delivers the first verifier: **short in-app MCQ assessments with 100% server-side scoring**, wired straight into the Skill Passport state machine that Phase 03 reserved (`SkillVerificationState`: `SelfDeclared → AssessmentVerified`, `EmployerVerified` reserved for Phase 06+ employer verification).

## Scope delivered

### Backend
- **Domain** (`KaamLink.Domain.Assessments`): `SkillAssessment` (one active template per skill, unique index), `AssessmentQuestion` (bilingual statement + 4 bilingual options; `CorrectOptionIndex` is server-only), `AssessmentAttempt` (scored only at submit; single-shot completion; full history), `AssessmentAnswer` (one answer per question, unique index). `WorkerSkill` gained `AssessmentVerifiedAt` + `MarkAssessmentVerified(now)` — the ONLY writer of the verified state.
- **Service** (`SkillAssessmentService`): list mine (availability + state + latest score), start (gates: profile required → onboarding complete → skill on profile → template exists), submit (ownership, single-shot, answer validation **against the same question set the template serves**, scoring, state transition, audit).
- **Scoring is fully server-side.** The start response's `AssessmentQuestionItem` contract deliberately has **no correct-answer field** (verified by an integration test that scans the raw HTTP payload).
- **Persistence**: 4 configurations, migration `20260824150000_Phase05Assessments` (+ `worker_skills.assessment_verified_at`), snapshot/designer regenerated with the Phase-04-learned navigation-ordering rule enforced by the verifier.
- **Seed** (`AssessmentCatalogSeeder`, idempotent): 3 question banks — **Welder**, **Aluminium Fabricator**, **Light Vehicle Driver** (5 questions each, EN+AR, pass mark 60%). Driver questions deliberately avoid specific UAE legal figures (fines/black points/limits) — those await professional legal review per the Master Prompt; we never invent legal requirements.
- **Endpoints** (`WorkerAssessmentsController`, authorized): `GET workers/me/assessments`, `POST workers/me/assessments/{skillId}/attempts`, `POST .../attempts/{attemptId}/submit`.

### Android
- **My assessments** (from Worker Home): every claimed skill with availability, state pill (`StatusColorConverter` extended: SelfDeclared/EmployerVerified/AssessmentVerified), latest score; honest "no assessment yet" rows are NOT hidden behind fake CTAs.
- **Assessment runner**: starts the attempt, renders options as large touch targets with selection state (new `SelectedOptionBackground/Text` converters), answered-count progress, single submit, and a **server-verdict panel** (score, pass/fail, what happens next). All strings EN+AR (23 new keys, 3-way parity enforced).

### Tests
- **12 unit** (`SkillAssessmentServiceTests`): scoring boundaries (100/67/33), state transition + audit, ownership 403, single-shot, invalid answers, template/​profile/​onboarding gates, retake = new attempt with history kept.
- **5 integration** (`AssessmentFlowTests`, real HTTP + PostgreSQL): full happy path **proving no correct answers leak in the payload**, pass → `/me` shows `AssessmentVerified`; honest 404/`skill_not_on_profile`/`onboarding_incomplete` gates; single-shot submit.

## Business rules (decisions, reviewable)

| Rule | Decision | Rationale |
|---|---|---|
| Who can take an assessment | Onboarded workers only, and only for skills **on their profile** | The assessment verifies a claim; without a claim there is nothing to verify |
| Retakes | Unlimited; every attempt stored and audited | Low-stakes V1 trust signal; history is never rewritten (§50) |
| Pass mark | 60% (seed config) | Deliberately modest V1 bar; admin tuning UI is a later phase |
| What a pass writes | `AssessmentVerified` + timestamp on the WorkerSkill | Single writer; employer rendering of the badge is Phase 06 surfacing |
| Unknown skills | Honest `assessment.not_available` | Never fake availability (§50) |

## Known limitations (accepted for V1)

1. Question banks exist for 3 of 20 skills; expansion is admin content work (no API change).
2. No time limits or question randomization (retake gaming is accepted at this trust level and noted in the roadmap).
3. `AssessmentVerified` is not yet rendered on employer-facing applicant cards (Phase 06 surfacing — the state is already in the DB and contracts).
4. No admin UI for managing templates/questions (seed-managed for now).

## Verification

- `tools/verify-phase05.py` — 88 checks: migration/snapshot/designer coherence, route + error-code parity, serializer registrations, state-token coverage, leak-guard presence in tests, navigation-ordering rule. Green.
- `tools/verify-phase03.py` (103) & `tools/verify-phase04.py` (105) — still green (no regressions).
- Formal gate on the user's machine: backend build + unit tests (72+12 = **84** expected), `dotnet ef migrations has-pending-model-changes` (expect *No model changes*), integration tests via Docker (20+5 = **25** expected), MAUI client build, emulator walkthrough: Worker Home → Skill assessments → Welder → start → answer → result → list shows *Assessment passed ✓*.

## Checkpoint

Per the Master Prompt: **STOP**. Do not begin Phase 06 (candidates: employer-visible verification surfacing, visit scheduling, vacancy expiry) until the user confirms Phase 05 is green on their machine.
