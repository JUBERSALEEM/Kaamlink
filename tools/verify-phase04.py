#!/usr/bin/env python3
"""
Phase 04 static cross-verification (runs without a .NET SDK).

Catches the classes of mistakes a compiler cannot: migration/snapshot/designer
coherence for the jobs tables, client↔backend route drift for the new endpoints,
enum-name mirrors (VacancyStatus/ApplicationStatus), DI/registration gaps for
the six new pages, and localization plumbing for the status pills.

It does NOT replace `dotnet build`/`dotnet test` — the first CLI/CI build is
the formal gate. Every FAILURE line must be fixed before Phase 04 closes.
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


# ------------------------------------------------------- backend: persistence
section("Backend: migration #3 coherence")
migration = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/20260824120000_Phase04Jobs.cs")
designer = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/20260824120000_Phase04Jobs.Designer.cs")
snapshot = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs")

for table in ("vacancies", "vacancy_skills", "job_applications"):
    check(f'name: "{table}"' in migration, f"migration creates {table}", f"migration missing {table}")
    check(f'b.ToTable("{table}")' in snapshot, f"snapshot maps {table}", f"snapshot missing {table}")
    check(f'b.ToTable("{table}")' in designer, f"designer maps {table}", f"designer missing {table}")

check('unique: true' in migration and "ux_job_applications_vacancy_worker" in migration,
      "migration has the unique (vacancy, worker) index", "migration missing unique application index")
check('IsUnique()' in snapshot and "ux_job_applications_vacancy_worker" in snapshot,
      "snapshot has the unique index", "snapshot missing unique application index")
check('[Migration("20260824120000_Phase04Jobs")]' in designer,
      "designer carries the migration id", "designer migration id wrong")
check(designer.count("[DbContext(typeof(AppDbContext))]") == 1,
      "designer has exactly one DbContext attribute", "designer DbContext attribute duplicated/missing")
check('HasColumnType("uuid")' in snapshot, "snapshot column types present", "snapshot missing column types")

for index in ("ix_vacancies_status_created_at", "ix_vacancies_company_id",
              "ix_vacancies_created_by_user_id", "ix_vacancy_skills_skill_id",
              "ix_job_applications_worker_profile_id"):
    check(index in migration and index in snapshot,
          f"index {index} in migration+snapshot", f"index {index} drift")

context = read("backend/src/KaamLink.Infrastructure/Persistence/AppDbContext.cs")
for dbset in ("Vacancies", "VacancySkills", "JobApplications"):
    check(f"DbSet<Domain.Jobs.{dbset.rstrip('s')}>" in context or f" {dbset} =>" in context,
          f"AppDbContext exposes {dbset}", f"AppDbContext missing {dbset}")

# ------------------------------------------------------ backend: registrations
section("Backend: DI + audit + policies")
app_di = read("backend/src/KaamLink.Application/DependencyInjection.cs")
infra_di = read("backend/src/KaamLink.Infrastructure/DependencyInjection.cs")
check("AddScoped<IVacancyService, VacancyService>()" in app_di,
      "IVacancyService registered", "IVacancyService missing in DI")
check("AddScoped<IJobApplicationService, JobApplicationService>()" in app_di,
      "IJobApplicationService registered", "IJobApplicationService missing in DI")
check("AddScoped<IVacancyRepository" in infra_di and "AddScoped<IJobApplicationRepository" in infra_di,
      "jobs repositories registered", "jobs repositories missing in DI")

audit = read("backend/src/KaamLink.Domain/Auditing/AuditActions.cs")
for action in ("VacancyCreated", "VacancyUpdated", "VacancyStatusChanged",
               "ApplicationSubmitted", "ApplicationStatusChanged", "ApplicationWithdrawn"):
    check(action in audit, f"audit action {action} central", f"audit action {action} missing")

services = read("backend/src/KaamLink.Application/Jobs/VacancyService.cs") + \
    read("backend/src/KaamLink.Application/Jobs/JobApplicationService.cs")
check("CompanyVerificationState.Verified" in services,
      "publishing gate uses verification state", "publishing gate missing")
check("jobs.already_applied" in services, "apply dedupe code present", "apply dedupe missing")
check("worker.onboarding_incomplete" in services, "onboarding gate present", "onboarding gate missing")
check("jobs.application_terminal" in services, "terminal guard present", "terminal guard missing")

# ---------------------------------------------------------- route parity
section("Client↔backend route parity (Phase 04)")
api_client = read("Core/Api/ApiClient.cs")
controllers = read("backend/src/KaamLink.Api/Controllers/VacanciesController.cs") + \
    read("backend/src/KaamLink.Api/Controllers/WorkerApplicationsController.cs") + \
    read("backend/src/KaamLink.Api/Controllers/CompanyVacanciesController.cs")

pairs = [
    ('"vacancies?"', '[HttpGet]', "feed"),
    ('"vacancies/{vacancyId}"', '[HttpGet(\"{id:guid}\")]', "detail"),
    ('/applications"', '[HttpPost(\"{id:guid}/applications\")]', "apply"),
    ('"workers/me/applications"', '[Route(\"api/v1/workers/me/applications\")]', "my applications"),
    ('/withdraw"', '[HttpPost(\"{id:guid}/withdraw\")]', "withdraw"),
    ('companies/{companyId}/vacancies"', "companies/{companyId:guid}/vacancies", "company vacancies route"),
    ('/status"', '[HttpPost(\"{vacancyId:guid}/status\")]', "vacancy status"),
    ('/applications/{applicationId}/status', '[HttpPost(\"{vacancyId:guid}/applications/{applicationId:guid}/status\")]',
     "application decision"),
]
for client_hint, server_hint, label in pairs:
    check(client_hint in api_client and server_hint in controllers,
          f"route pair: {label}", f"route drift: {label}")

# --------------------------------------------------- enum mirrors (contract)
section("Enum-name mirrors client↔server")
enum_display = read("Core/Presentation/EnumDisplay.cs")
for status in ("Draft", "Active", "Paused", "Closed"):
    check(f'"{status}"' in enum_display, f"VacancyStatus {status} mirrored client-side",
          f"VacancyStatus {status} missing in EnumDisplay")
for status in ("Applied", "Viewed", "Shortlisted", "Rejected", "Hired", "Withdrawn"):
    check(f'"{status}"' in enum_display, f"ApplicationStatus {status} mirrored client-side",
          f"ApplicationStatus {status} missing in EnumDisplay")
for enum_file, members in (
    ("backend/src/KaamLink.Domain/Jobs/VacancyStatus.cs", ("Draft", "Active", "Paused", "Closed")),
    ("backend/src/KaamLink.Domain/Jobs/ApplicationStatus.cs",
     ("Applied", "Viewed", "Shortlisted", "Rejected", "Hired", "Withdrawn")),
):
    text = read(enum_file)
    for member in members:
        check(re.search(rf"^\s*{member},?\s*$", text, re.MULTILINE) is not None,
              f"server enum member {member}", f"server enum missing {member}")

# --------------------------------------------------------- client: plumbing
section("Client: registrations + serializer")
shell = read("AppShell.xaml")
maui = read("MauiProgram.cs")
for route in ("vacancyfeed", "vacancydetail", "myapplications",
              "companyvacancies", "vacancyedit", "vacancyapplications"):
    check(f'Route="{route}"' in shell, f"Shell route {route}", f"Shell missing route {route}")
for page in ("VacancyFeedPage", "VacancyDetailPage", "MyApplicationsPage",
             "CompanyVacanciesPage", "VacancyEditPage", "VacancyApplicationsPage"):
    check(f"AddTransient<{page}>()" in maui, f"{page} in DI", f"{page} missing in DI")

contracts = read("Core/Api/ApiContracts.cs")
for dto in ("PagedResponseDto<VacancyFeedItemDto>", "VacancyDetailDto", "UpsertVacancyRequestDto",
            "IReadOnlyList<CompanyVacancySummaryDto>", "IReadOnlyList<ApplicationItemDto>",
            "IReadOnlyList<VacancyApplicationItemDto>", "ApplicationStatusChangeDto"):
    check(f"JsonSerializable(typeof({dto}))" in contracts,
          f"serializer covers {dto}", f"serializer missing {dto}")

app_xaml = read("App.xaml")
check('x:Key="StatusColorConverter"' in app_xaml,
      "StatusColorConverter registered in App.xaml", "StatusColorConverter not registered")

pages = read("Features/Jobs/VacancyDetailPage.xaml") + \
    read("Features/Jobs/MyApplicationsPage.xaml") + \
    read("Features/Jobs/CompanyVacanciesPage.xaml") + \
    read("Features/Jobs/VacancyApplicationsPage.xaml")
check(pages.count("StatusColorConverter") >= 4,
      "status pills use the shared converter", "status pills not wired")

# ------------------------------------------------------------ resx spot keys
section("Localization spot keys (parity itself is enforced by verify-phase03.py)")
resx = read("Resources/Strings/AppStrings.resx")
designer = read("Resources/Strings/AppStrings.Designer.cs")
for key in ("FeedTitle", "ApplyCta", "AppliedBadgeFormat", "ErrorAlreadyApplied",
            "ErrorCompanyNotVerified", "MyApplicationsTitle", "WithdrawCta",
            "CompanyVacanciesTitle", "NewVacancyCta", "VacancyEditTitleNew",
            "FieldHeadcountLabel", "FieldSkillsLabel", "VacancyApplicationsTitle",
            "ActionShortlist", "ActionHire", "ManageVacanciesCta", "BrowseJobsCta"):
    check(f'name="{key}"' in resx and f"string {key} =>" in designer,
          f"key {key} resx+designer", f"key {key} missing")

# --------------------------------------- repo-root single-project glob guard
section("Repo-root glob guard (KaamLink.csproj must not compile backend/**)")
csproj = read("KaamLink.csproj")
check('<Compile Remove="backend\\**\\*.cs"' in csproj,
      "KaamLink.csproj excludes backend/**/*.cs from Compile",
      "KaamLink.csproj swallowed backend sources (VS CS0246 flood) — re-add Compile Remove for backend")
check('<None Remove="backend\\**\\*"' in csproj,
      "KaamLink.csproj excludes backend/** from None items",
      "KaamLink.csproj missing None Remove for backend tree")

# Snapshot/designer model-builder lines execute in FILE ORDER at runtime.
# A bare b.Navigation("N") is a LOOKUP: it throws InvalidOperationException unless
# a creating call (HasOne/HasMany/WithOne/WithMany with the same name) already ran
# on an earlier line. (Caught live: Vacancy-block Navigation("Skills") referenced a
# navigation only created later in the VacancySkill block — API crashed at startup
# during HasPendingModelChanges.)
section("Snapshot/designer navigation ordering")
for label, text in (("snapshot", snapshot), ("designer", designer)):
    violations = []
    for lineno, line in enumerate(text.splitlines(), start=1):
        m = re.search(r'b\.Navigation\("(\w+)"\)', line)
        if not m:
            continue
        nav = m.group(1)
        creator = re.compile(
            r'(WithMany|WithOne)\("' + re.escape(nav) + r'"\)'
            r'|Has(One|Many)\("[^"]+",\s*"' + re.escape(nav) + r'"\)')
        if not any(creator.search(prior) for prior in text.splitlines()[:lineno - 1]):
            violations.append(f"{label}:{lineno} Navigation(\"{nav}\") has no creator on an earlier line")
    check(not violations, f"{label} navigation lookups resolve before use",
          "; ".join(violations) if violations else f"{label} navigation ordering broken")

# ---------------------------------------------------------------- summary
print(f"\n{'=' * 60}\n{checks} checks, {len(failures)} failures")
if failures:
    print("FAILURES:")
    for failure in failures:
        print(f"  - {failure}")
    sys.exit(1)
print("ALL STATIC CHECKS PASSED")
