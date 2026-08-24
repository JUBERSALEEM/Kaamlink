#!/usr/bin/env python3
"""
Phase 05 static cross-verification (runs without a .NET SDK).

Catches drift a compiler cannot: migration/snapshot/designer coherence for the
assessment tables + worker_skills column, client↔backend route & error-code parity,
serializer/DI registrations, design-token coverage for the new states, and the
snapshot navigation-ordering rule learned the hard way in Phase 04's first boot.

It does NOT replace `dotnet build`/`dotnet test` — the first CLI run is the gate.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
failures = []
checks = 0


def check(condition, ok_msg, fail_msg):
    global checks
    checks += 1
    if condition:
        print(f"  ok   {ok_msg}")
    else:
        print(f"  FAIL {fail_msg}")
        failures.append(fail_msg)


def read(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as handle:
        return handle.read()


def section(title):
    print(f"\n== {title}")


# ------------------------------------------------------------- backend core
section("Backend: domain + service + error codes")
for path in (
    "backend/src/KaamLink.Domain/Assessments/SkillAssessment.cs",
    "backend/src/KaamLink.Domain/Assessments/AssessmentQuestion.cs",
    "backend/src/KaamLink.Domain/Assessments/AssessmentAttempt.cs",
    "backend/src/KaamLink.Domain/Assessments/AssessmentAnswer.cs",
    "backend/src/KaamLink.Application/Assessments/SkillAssessmentService.cs",
    "backend/src/KaamLink.Application/Abstractions/IAssessmentRepository.cs",
    "backend/src/KaamLink.Infrastructure/Assessments/AssessmentRepository.cs",
    "backend/src/KaamLink.Api/Controllers/WorkerAssessmentsController.cs",
):
    check(os.path.exists(os.path.join(ROOT, path)), f"exists {os.path.basename(path)}", f"missing {path}")

worker_skill = read("backend/src/KaamLink.Domain/Workers/WorkerSkill.cs")
check("MarkAssessmentVerified" in worker_skill and "AssessmentVerifiedAt" in worker_skill,
      "WorkerSkill: AssessmentVerifiedAt + MarkAssessmentVerified",
      "WorkerSkill assessment marking missing")

service = read("backend/src/KaamLink.Application/Assessments/SkillAssessmentService.cs")
client_codes = read("Core/Api/ApiClient.cs")
for code in ("assessment.not_available", "assessment.skill_not_on_profile", "assessment.already_completed",
             "assessment.attempt_forbidden", "assessment.question_invalid", "assessment.answer_invalid"):
    check(f'"{code}"' in service, f"service throws {code}", f"service missing {code}")
client_maps = [c for c in ("assessment.not_available", "assessment.skill_not_on_profile",
                           "assessment.already_completed") if c in client_codes]
check(len(client_maps) == 3, "client Codes mirror the contract codes", "client Codes missing assessment codes")

check("AssessmentQuestionItem" in read("backend/src/KaamLink.Application/Assessments/AssessmentsContracts.cs")
      and "CorrectOptionIndex" not in read("backend/src/KaamLink.Application/Assessments/AssessmentsContracts.cs"),
      "read contract excludes correct answers (no CorrectOptionIndex in DTOs)",
      "read contract leaks CorrectOptionIndex")

audit = read("backend/src/KaamLink.Domain/Auditing/AuditActions.cs")
for const_name, action in (("AssessmentAttemptStarted", "assessment.attempt_started"),
                           ("AssessmentSubmitted", "assessment.submitted"),
                           ("WorkerSkillAssessmentVerified", "worker.skill_assessment_verified")):
    check(f'"{action}"' in audit and f"AuditActions.{const_name}" in service,
          f"audit action {action} wired", f"audit action {action} missing")

# ----------------------------------------------------------- persistence
section("Backend: migration #4 coherence")
migration = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/20260824150000_Phase05Assessments.cs")
designer = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/20260824150000_Phase05Assessments.Designer.cs")
snapshot = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs")

for table in ("skill_assessments", "assessment_questions", "assessment_attempts", "assessment_answers"):
    check(f'name: "{table}"' in migration, f"migration creates {table}", f"migration missing {table}")
    check(f'b.ToTable("{table}")' in snapshot, f"snapshot maps {table}", f"snapshot missing {table}")
    check(f'b.ToTable("{table}")' in designer, f"designer maps {table}", f"designer missing {table}")

check('name: "assessment_verified_at"' in migration and "worker_skills" in migration,
      "migration adds worker_skills.assessment_verified_at", "migration missing worker_skills column")
check("AssessmentVerifiedAt" in snapshot and "AssessmentVerifiedAt" in designer,
      "snapshot/designer map AssessmentVerifiedAt", "worker_skills column not in snapshot/designer")
check("ux_skill_assessments_skill_id" in migration and "ux_skill_assessments_skill_id" in snapshot,
      "one-template-per-skill unique index", "unique template index missing")
check("ux_assessment_answers_attempt_question" in migration and "ux_assessment_answers_attempt_question" in snapshot,
      "one-answer-per-question unique index", "unique answer index missing")
check('[Migration("20260824150000_Phase05Assessments")]' in designer
      and designer.count("[DbContext(typeof(AppDbContext))]") == 1,
      "designer attrs", "designer migration id / DbContext attribute wrong")

for label, text in (("snapshot", snapshot), ("designer", designer)):
    violations = []
    lines = text.splitlines()
    for lineno, line in enumerate(lines, start=1):
        m = re.search(r'b\.Navigation\("(\w+)"\)', line)
        if not m:
            continue
        nav = m.group(1)
        prior = "\n".join(lines[:lineno - 1])
        if not re.search(r'(WithMany|WithOne)\("' + re.escape(nav) + r'"\)'
                         + r'|Has(One|Many)\("[^"]+",\s*"' + re.escape(nav) + r'"\)', prior):
            violations.append(f"{label}:{lineno} Navigation(\"{nav}\")")
    check(not violations, f"{label} navigation ordering", "; ".join(violations) if violations else "ordering broken")

# ------------------------------------------------------------- seeding + DI
section("Backend: seeding + DI wiring")
seeder = read("backend/src/KaamLink.Infrastructure/Persistence/Seeding/AssessmentCatalogSeeder.cs")
check(seeder.count("QuestionSeed[]") == 3, "seeder has 3 question banks", "seeder banks != 3")
check("EnsureSeededAsync" in seeder and "SkillAssessments.AnyAsync" in seeder,
      "seeder is idempotent", "seeder missing idempotence guard")
startup = read("backend/src/KaamLink.Api/Configuration/StartupTasks.cs")
check("AssessmentCatalogSeeder.EnsureSeededAsync" in startup, "seeder called from StartupTasks", "seeder not wired")
app_di = read("backend/src/KaamLink.Application/DependencyInjection.cs")
infra_di = read("backend/src/KaamLink.Infrastructure/DependencyInjection.cs")
check("ISkillAssessmentService" in app_di, "service registered (Application DI)", "service DI missing")
check("IAssessmentRepository" in infra_di, "repository registered (Infrastructure DI)", "repo DI missing")
dbctx = read("backend/src/KaamLink.Infrastructure/Persistence/AppDbContext.cs")
for dbset in ("SkillAssessments", "AssessmentQuestions", "AssessmentAttempts", "AssessmentAnswers"):
    check(dbset in dbctx, f"DbSet {dbset}", f"DbSet {dbset} missing")

# ------------------------------------------------------------- routes
section("Routes: server ↔ client ↔ shell")
controller = read("backend/src/KaamLink.Api/Controllers/WorkerAssessmentsController.cs")
check('Route("api/v1/workers/me/assessments")' in controller, "controller route", "controller route wrong")
api = read("Core/Api/ApiClient.cs")
for segment in ("workers/me/assessments", "attempts", "submit"):
    check(segment in api, f"ApiClient calls .../{segment}", f"ApiClient missing {segment}")
routes = read("Core/Navigation/AppRoutes.cs")
shell = read("AppShell.xaml")
for route, page in (("myassessments", "MyAssessmentsPage"), ("assessmentrun", "AssessmentRunPage")):
    check(f'"{route}"' in routes, f"AppRoutes.{route}", f"AppRoutes missing {route}")
    check(f'Route="{route}"' in shell and page in shell, f"Shell {route}→{page}", f"Shell missing {route}")

maui = read("MauiProgram.cs")
for reg in ("MyAssessmentsPage", "MyAssessmentsViewModel", "AssessmentRunPage", "AssessmentRunViewModel"):
    check(reg in maui and "Features.Assessments" in maui, f"DI {reg}", f"DI missing {reg}")

# ------------------------------------------------------------- serializer
section("Serializer coverage (AppJsonContext)")
contracts = read("Core/Api/ApiContracts.cs")
for dto in ("MyAssessmentItemDto", "IReadOnlyList<MyAssessmentItemDto>", "AssessmentQuestionItemDto",
            "StartAssessmentResponseDto", "SubmitAssessmentRequestDto", "AssessmentResultDto"):
    check(f"[JsonSerializable(typeof({dto}))]" in contracts, f"serializable {dto}", f"miss-serialized {dto}")

# ------------------------------------------------------------- UX tokens
section("UX: converters + state tokens")
converters = read("Core/Presentation/Converters.cs")
check('"AssessmentVerified" => "Success"' in converters, "StatusColorConverter knows AssessmentVerified", "state color missing")
for conv in ("SelectedOptionBackgroundConverter", "SelectedOptionTextColorConverter"):
    check(conv in converters, f"{conv} exists", f"{conv} missing")
appxaml = read("App.xaml")
for key in ("SelectedOptionBackground", "SelectedOptionText"):
    check(f'x:Key="{key}"' in appxaml, f"App.xaml resource {key}", f"App.xaml missing {key}")
run_page = read("Features/Assessments/AssessmentRunPage.xaml")
check("SelectedOptionBackground" in run_page and "SelectedOptionText" in run_page,
      "runner options wired to converters", "runner option converters not wired")

# ------------------------------------------------------------- resx keys
section("Localization spot keys (parity is enforced by verify-phase03.py)")
resx = read("Resources/Strings/AppStrings.resx")
designer = read("Resources/Strings/AppStrings.Designer.cs")
for key in ("AssessmentsTitle", "MyAssessmentsCta", "AssessmentStartCta", "AssessmentNotAvailable",
            "StateAssessmentVerified", "AssessmentResultScoreFormat", "AssessmentPassMarkFormat",
            "AssessmentSubmitCta", "AssessmentDoneCta", "ErrorAssessmentNotAvailable",
            "ErrorAssessmentSkillNotOnProfile", "ErrorAssessmentAlreadyCompleted"):
    check(f'name="{key}"' in resx and f"string {key} =>" in designer,
          f"key {key} resx+designer", f"key {key} missing")

# ------------------------------------------------------------- tests
section("Tests exist and reference the service")
unit = read("backend/tests/KaamLink.UnitTests/Assessments/SkillAssessmentServiceTests.cs")
check(unit.count("[Fact]") >= 10, f"unit facts present ({unit.count('[Fact]')})", "too few unit tests")
check("AssessmentVerified" in unit, "unit asserts verification transition", "unit missing state assertion")
integ = read("backend/tests/KaamLink.IntegrationTests/AssessmentFlowTests.cs")
check(integ.count("[Fact]") >= 5, f"integration facts present ({integ.count('[Fact]')})", "too few integration tests")
check('Assert.DoesNotContain("correct"' in integ, "integration proves no correct-answer leak", "leak guard missing")

# ---------------------------------------------------------------- summary
print(f"\n{'=' * 60}\n{checks} checks, {len(failures)} failures")
if failures:
    print("FAILURES:")
    for failure in failures:
        print(f"  - {failure}")
    sys.exit(1)
print("ALL STATIC CHECKS PASSED")
