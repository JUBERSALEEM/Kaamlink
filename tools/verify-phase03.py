#!/usr/bin/env python3
"""
Phase 03 static cross-verification (runs without a .NET SDK).

It does NOT replace `dotnet build`/`dotnet test` — it catches the classes of
mistakes that a compiler cannot: config/migration/snapshot/index coherence,
resx↔designer↔XAML string-key drift, DI registration gaps, route drift and
client↔backend DTO contract drift. Every FAILURE line must be fixed before
Phase 03 is considered done; the first CI run is the formal gate.
"""
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

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


# ---------------------------------------------------------------- backend
section("Backend: XML/JSON validity")
for csproj in [
    "backend/src/KaamLink.Domain/KaamLink.Domain.csproj",
    "backend/src/KaamLink.Application/KaamLink.Application.csproj",
    "backend/src/KaamLink.Infrastructure/KaamLink.Infrastructure.csproj",
    "backend/src/KaamLink.Api/KaamLink.Api.csproj",
    "KaamLink.csproj",
]:
    try:
        ET.fromstring(read(csproj))
        check(True, f"{csproj} is well-formed XML", "")
    except Exception as exc:  # noqa: BLE001
        check(False, "", f"{csproj} XML broken: {exc}")

settings = {name: json.loads(read(f"backend/src/KaamLink.Api/{name}"))
            for name in ("appsettings.json", "appsettings.Development.json")}
check("Admin" in settings["appsettings.json"], "appsettings.json has Admin section",
      "appsettings.json missing Admin section")
check("+971500000000" in json.dumps(settings["appsettings.Development.json"]),
      "Development admin bootstrap phone present",
      "appsettings.Development.json missing admin bootstrap phone")

section("Backend: migration2 ↔ snapshot ↔ configurations coherence")
migration = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/20260823130000_Phase03ProfilesCompanies.cs")
snapshot = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs")
designer = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/20260823130000_Phase03ProfilesCompanies.Designer.cs")

tables = ["worker_profiles", "worker_skills", "companies", "company_members", "skill_categories", "skills"]
for table in tables:
    check(f'"{table}"' in migration, f"migration creates {table}", f"migration missing {table}")
    check(f'"{table}"' in snapshot, f"snapshot has {table}", f"snapshot missing {table}")

# Migrations name columns via anonymous-member syntax (`col = table.Column<T>()`);
# the snapshot uses HasColumnName("col"). Accept both spellings on the migration side.
for column in ["profile_completeness_pct", "onboarding_complete", "primary_skill_id",
               "verification_state", "verification_reason", "verified_at",
               "trade_license_number", "years_experience", "is_primary",
               "requires_certification", "name_ar", "sort_order"]:
    in_migration = re.search(rf'(?:"{column}"|\b{column}\s*=)', migration) is not None
    check(in_migration and f'"{column}"' in snapshot,
          f"column {column} coherent", f"column {column} missing in migration or snapshot")

for index in ["ux_companies_trade_license_number", "ix_company_members_user_id",
              "ix_worker_skills_skill_id", "ix_worker_profiles_primary_skill_id"]:
    check(index in migration and index in snapshot,
          f"index {index} coherent", f"index {index} missing in migration or snapshot")

check('HasFilter("trade_license_number IS NOT NULL")' in designer
      or 'HasFilter("trade_license_number IS NOT NULL")' in snapshot,
      "filtered licence unique index uses a filter",
      "filtered licence index missing its filter")

seeder = read("backend/src/KaamLink.Infrastructure/Persistence/Seeding/SkillCatalogSeeder.cs")
check(seeder.count("Guid.Parse") == 26, "seeder has 6 categories + 20 skills with fixed GUIDs",
      f"seeder GUID count off: {seeder.count('Guid.Parse')}")
check(seeder.count(", true),") == 1, "exactly one skill requires certification (driver)",
      "seeder certification flags wrong")
for name in ("Aluminium Fabricator", "Welder", "Light Vehicle Driver"):
    check(f'"{name}"' in seeder, f"seed contains '{name}'", f"seed missing '{name}'")

section("Backend: services/options/tests wiring")
auth_ctor = re.search(r"public sealed class AuthService\((.*?)\)", read(
    "backend/src/KaamLink.Application/Auth/AuthService.cs"), re.S)
harness_ctor = re.search(r"public AuthService CreateService\(\) => new\((.*?)\);", read(
    "backend/tests/KaamLink.UnitTests/Fakes.cs"), re.S)
auth_args = [a.strip() for a in auth_ctor.group(1).split(",") if a.strip()]
harness_args = [a.strip() for a in harness_ctor.group(1).split(",") if a.strip()]
check(len(auth_args) == 13, "AuthService has 13 ctor deps", f"AuthService deps = {len(auth_args)}")
check(len(harness_args) == len(auth_args), "test harness passes same arg count",
      f"harness args {len(harness_args)} != service args {len(auth_args)}")

di_infra = read("backend/src/KaamLink.Infrastructure/DependencyInjection.cs")
for reg in ("IWorkerProfileRepository", "ICompanyRepository", "ISkillCatalogRepository"):
    check(reg in di_infra, f"DI registers {reg}", f"DI missing {reg}")
di_app = read("backend/src/KaamLink.Application/DependencyInjection.cs")
for reg in ("IWorkerProfileService", "ICompanyService", "ISkillCatalogService"):
    check(reg in di_app, f"DI registers {reg}", f"DI missing {reg}")

program = read("backend/src/KaamLink.Api/Program.cs")
check("AdminOptions" in program and "AddSingleton(adminOptions)" in program,
      "AdminOptions bound + registered in Program.cs", "Program.cs missing AdminOptions wiring")
startup = read("backend/src/KaamLink.Api/Configuration/StartupTasks.cs")
check("EnsureReferenceDataSeededAsync" in startup and "SkillCatalogSeeder" in startup,
      "startup seeds the catalog", "startup seed task missing")
check("EnsureReferenceDataSeededAsync" in program, "Program invokes the seed task",
      "Program.cs never invokes the seed task")

factory = read("backend/tests/KaamLink.IntegrationTests/KaamLinkApiFactory.cs")
check('"Admin:BootstrapPhones:0"' in factory, "integration factory sets admin bootstrap phone",
      "integration factory missing admin bootstrap override")

flow = read("backend/tests/KaamLink.IntegrationTests/OnboardingFlowTests.cs")
for code in ('"Workshop"', '"ConstructionContracting"', '"ManufacturingFactory"',
             '"Light Vehicle Driver"', "AdminPhone.Number"):
    check(code in flow, f"integration tests use valid contract value {code}",
          f"integration tests use invalid value {code}")

bad = read("backend/tests/KaamLink.UnitTests/Companies/CompanyServiceTests.cs")
check("harnessAuditOf" not in bad, "CompanyServiceTests has no stray helper artifacts",
      "CompanyServiceTests still contains compile-breaking helper artifact")
fakes = read("backend/tests/KaamLink.UnitTests/Fakes.cs")
check("InMemorySkillCatalogRepository" in fakes and "SeedTestCatalog" in fakes,
      "fakes include the skill-catalog repo + seed helper",
      "fakes missing InMemorySkillCatalogRepository/SeedTestCatalog")

section("Backend: no app secrets committed")
# Phase-02 design decision (docs/phase-02-report.md): the ONLY committed passwords
# are throwaway local-docker dev placeholders (kaamlink/kaamlink, postgres/postgres),
# explicitly rejected by Production startup guards. Anything else is a failure.
DEV_PASSWORD_ALLOWLIST = {"kaamlink", "postgres"}
leak = []
for name, content in settings.items():
    flat = json.dumps(content)
    if "-----BEGIN" in flat:
        leak.append(f"{name}: private key block")
    for match in re.findall(r"Password=([^;\"]+)", flat):
        if match not in DEV_PASSWORD_ALLOWLIST:
            leak.append(f"{name}: Password={match[:4]}***")
check(not leak, "no real secrets in appsettings (dev placeholders allowlisted)",
      f"possible committed secrets: {leak}")
compose = read("backend/docker-compose.yml")
real_pw = [pw for pw in re.findall(r"Password=([^;\"]+)", compose) if pw not in DEV_PASSWORD_ALLOWLIST]
check(not real_pw, "compose uses only dev-placeholder passwords",
      f"compose contains a real-looking password: {real_pw}")

# ---------------------------------------------------------------- client
section("Client: XAML well-formedness")
xaml_files = []
for base, _, files in os.walk(ROOT):
    if ".git" in base or os.sep + "backend" in base:
        continue
    for fname in files:
        if fname.endswith(".xaml"):
            xaml_files.append(os.path.relpath(os.path.join(base, fname), ROOT))
xaml_docs = {}
for fname in sorted(xaml_files):
    try:
        xaml_docs[fname] = ET.fromstring(read(fname))
        check(True, f"{fname} parses", "")
    except Exception as exc:  # noqa: BLE001
        check(False, "", f"{fname} broken XAML: {exc}")

section("Client: resx ↔ designer ↔ XAML string-key parity")


def resx_keys(path):
    return set(re.findall(r'<data name="([^"]+)"', read(path)))


en_keys = resx_keys("Resources/Strings/AppStrings.resx")
ar_keys = resx_keys("Resources/Strings/AppStrings.ar.resx")
check(en_keys == ar_keys, "EN and AR resx have identical key sets",
      f"resx drift EN-only={sorted(en_keys - ar_keys)} AR-only={sorted(ar_keys - en_keys)}")

designer = read("Resources/Strings/AppStrings.Designer.cs")
designer_props = set(re.findall(r"public static string (\w+) =>", designer))
check(en_keys == designer_props, "designer exposes every resx key",
      f"designer drift missing={sorted(en_keys - designer_props)} extra={sorted(designer_props - en_keys)}")

used_static = set()
for fname, doc in xaml_docs.items():
    text = read(fname)
    used_static |= set(re.findall(r"x:Static strings:AppStrings\.(\w+)", text))
check(used_static <= en_keys, "all XAML x:Static string refs exist",
      f"XAML references unknown keys: {sorted(used_static - en_keys)}")

used_cs = set()
for base, _, files in os.walk(os.path.join(ROOT, "Core")):
    for fname in files:
        if fname.endswith(".cs"):
            used_cs |= set(re.findall(r"AppStrings\.(\w+)", read(os.path.join(base, fname))))
used_cs -= {"ResourceManager", "Culture"}
for base, _, files in os.walk(os.path.join(ROOT, "Features")):
    for fname in files:
        if fname.endswith(".cs"):
            used_cs |= set(re.findall(r"AppStrings\.(\w+)", read(os.path.join(base, fname))))
check(used_cs <= en_keys, "all C# AppStrings refs exist",
      f"C# references unknown keys: {sorted(used_cs - en_keys)}")

section("Client: StaticResource keys resolve")
declared = set()
for fname in ("Resources/Styles/Icons.xaml", "Resources/Styles/Colors.xaml",
              "Resources/Styles/Styles.xaml", "App.xaml"):
    declared |= set(re.findall(r'x:Key="([^"]+)"', read(fname)))
implicit = {  # MAUI framework resources always present
    "InvertedBoolConverter",
}
resolved_ok = declared | implicit
used_resources = set()
for fname in sorted(xaml_docs):
    used_resources |= set(re.findall(r"\{StaticResource ([\w]+)\}", read(fname)))
missing = used_resources - resolved_ok
check(not missing, "all StaticResource keys declared",
      f"undeclared StaticResource keys: {sorted(missing)}")

section("Client: routes ↔ AppRoutes ↔ navigation calls")
appshell = read("AppShell.xaml")
shell_routes = set(re.findall(r'Route="([^"]+)"', appshell))
app_routes_cs = read("Core/Navigation/AppRoutes.cs")
route_consts = set(re.findall(r'public const string \w+ = "([^"]+)";', app_routes_cs))
check(shell_routes == route_consts, "AppShell routes == AppRoutes constants",
      f"route drift shell={sorted(shell_routes)} consts={sorted(route_consts)}")
old_pattern = "Routing.RegisterRoute" in read("AppShell.xaml.cs")
check(not old_pattern, "AppShell.xaml.cs has no leftover RegisterRoute calls",
      "AppShell.xaml.cs still registers routes for non-hierarchy pages")

section("Client: DI registrations cover every page + VM")
program_di = read("MauiProgram.cs")
needed = [
    "RoleSelectionPage", "RoleSelectionViewModel",
    "LoginPage", "LoginViewModel", "OtpPage", "OtpViewModel",
    "WorkerOnboardingPage", "WorkerOnboardingViewModel",
    "WorkerHomePage", "WorkerHomeViewModel",
    "EmployerOnboardingPage", "EmployerOnboardingViewModel",
    "EmployerHomePage", "EmployerHomeViewModel",
    "ITokenStore", "ISessionService", "IApiClient", "AuthHeaderHandler",
]
missing_di = [name for name in needed if name not in program_di]
check(not missing_di, "MauiProgram registers all services",
      f"MauiProgram missing registrations: {missing_di}")

section("Client: bindings reference real VM members (typos net)")
page_vm_map = {
    "Features/Onboarding/RoleSelectionPage.xaml": "Features/Onboarding/RoleSelectionViewModel.cs",
    "Features/Auth/LoginPage.xaml": "Features/Auth/LoginViewModel.cs",
    "Features/Auth/OtpPage.xaml": "Features/Auth/OtpViewModel.cs",
    "Features/Worker/WorkerOnboardingPage.xaml": "Features/Worker/WorkerOnboardingViewModel.cs",
    "Features/Worker/WorkerHomePage.xaml": "Features/Worker/WorkerHomeViewModel.cs",
    "Features/Employer/EmployerOnboardingPage.xaml": "Features/Employer/EmployerOnboardingViewModel.cs",
    "Features/Employer/EmployerHomePage.xaml": "Features/Employer/EmployerHomeViewModel.cs",
}
binding_re = re.compile(r"\{Binding (?:Path=|BindingContext\.|[A-Za-z]*[=:, ]?)*\b([A-Za-z]\w+)")
skip_identifiers = {"Binding", "Converter", "Source", "RelativeSource", "AncestorType",
                    "StaticResource", "FallbackValue", "x", "Type", "Static", "strings",
                    "Path", "Mode", "StringFormat", "CommandParameter", "ItemDisplayBinding"}
for page, vm_file in page_vm_map.items():
    page_src = read(page)
    vm_src = read(vm_file)
    model_extras = ""
    sibling = os.path.dirname(vm_file)
    for fname in os.listdir(os.path.join(ROOT, sibling)):
        if fname.endswith("Models.cs") or fname.endswith("ViewModel.cs"):
            model_extras += read(os.path.join(sibling, fname))
    unknown = set()
    for match in re.finditer(r"\{Binding\s+([^}]+)\}", page_src):
        expr = match.group(1)
        # Candidate member: after Path=/BindingContext. or the leading identifier.
        target = None
        path_match = re.search(r"(?:Path=|BindingContext\.)([A-Za-z]\w+)", expr)
        if path_match:
            target = path_match.group(1)
        else:
            lead = re.match(r"([A-Za-z]\w+)", expr.split(",")[0].strip())
            if lead:
                target = lead.group(1)
        if not target or target in skip_identifiers:
            continue
        if target in vm_src or target in model_extras:
            continue
        # Source-generated members: [RelayCommand] XxxAsync() → XxxCommand,
        # [ObservableProperty] _foo → Foo. Match against the method/field spelling.
        if target.endswith("Command"):
            method = target[:-7]
            if method in vm_src or (method + "Async") in vm_src:
                continue
        if "_%s" % target[:1].lower() + target[1:] in vm_src:
            continue
        unknown.add(target)
    check(not unknown, f"{page} bindings resolve",
          f"{page} unknown bindings: {sorted(unknown)}")

section("Client: DTO contracts mirror the backend (camelCase)")
mirror_pairs = [
    ("backend/src/KaamLink.Application/Auth/AuthContracts.cs", "record AuthTokensResponse",
     "Core/Api/ApiContracts.cs", "record AuthTokensResponseDto"),
    ("backend/src/KaamLink.Application/Workers/WorkerContracts.cs", "record WorkerProfileResponse",
     "Core/Api/ApiContracts.cs", "record WorkerProfileResponseDto"),
    ("backend/src/KaamLink.Application/Companies/CompanyContracts.cs", "record CompanySummaryResponse",
     "Core/Api/ApiContracts.cs", "record CompanySummaryDto"),
]
contract_drift = []
for be_path, be_marker, fe_path, fe_marker in mirror_pairs:
    be = read(be_path)
    fe = read(fe_path)
    be_props = re.findall(r"\b(?:string|Guid|int|bool|DateTimeOffset|IReadOnlyList<[\w]+>)\??\s+(\w+)\s*,?",
                          be.split(be_marker)[1].split(")")[0])
    fe_props = re.findall(r"\b(?:string|Guid|int|bool|DateTimeOffset|IReadOnlyList<[\w]+>)\??\s+(\w+)\s*,?",
                          fe.split(fe_marker)[1].split(")")[0])
    be_camel = {p[0].lower() + p[1:] for p in be_props}
    fe_camel = {p[0].lower() + p[1:] for p in fe_props}
    for prop in sorted(set(fe_camel) - set(be_camel)):
        contract_drift.append(f"{fe_marker}.{prop} not on backend")
print(f"  (contract spot-check compared {len(mirror_pairs)} record pairs)")
check(not contract_drift, "client DTO fields ⊆ backend contract fields",
      f"DTO drift: {contract_drift}")

section("Client: platform files")
manifest = read("Platforms/Android/AndroidManifest.xml")
check('android:networkSecurityConfig="@xml/network_security_config"' in manifest,
      "manifest references the network security config", "manifest missing networkSecurityConfig")
nsc = read("Platforms/Android/Resources/xml/network_security_config.xml")
check(nsc.count("cleartextTrafficPermitted=\"true\"") == 1 and "10.0.2.2" in nsc,
      "cleartext limited to the dev-machine domains", "network security config too permissive")
csproj = read("KaamLink.csproj")
check("Microsoft.Extensions.Http" in csproj, "client references Microsoft.Extensions.Http",
      "client missing Microsoft.Extensions.Http package reference")
check("net10.0-android" in csproj and "net10.0-ios" not in csproj,
      "Android-only target", "unexpected extra target frameworks")

# ------------------------------------------------- backend: test compilation
# Xunit is NOT covered by ImplicitUsings — regressions here surface as CS0246
# storms ([Fact]/[Theory]/Assert/InlineData "could not be found") locally.
section("Backend: Xunit visibility in test projects")
for test_project in (
    "backend/tests/KaamLink.UnitTests",
    "backend/tests/KaamLink.IntegrationTests",
):
    global_usings_path = os.path.join(ROOT, test_project, "GlobalUsings.cs")
    has_global_xunit = (
        os.path.isfile(global_usings_path)
        and "global using Xunit" in read(os.path.join(test_project, "GlobalUsings.cs"))
    )
    check(has_global_xunit,
          f"{test_project}/GlobalUsings.cs exposes Xunit",
          f"{test_project} missing GlobalUsings.cs with 'global using Xunit;'")

# ------------------------------------------------------------- summary
print(f"\n{'=' * 60}\n{checks} checks, {len(failures)} failures")
if failures:
    print("FAILURES:")
    for failure in failures:
        print(f"  - {failure}")
    sys.exit(1)
print("ALL STATIC CHECKS PASSED")
