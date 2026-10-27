#!/usr/bin/env python3
"""Static gate for Supabase email OTP + password auth, terms gating and Aurora Clay.

It does not replace `dotnet build`, backend integration tests, hosted Supabase
template verification or a device OTP round trip. It catches cross-file drift
that would otherwise ship an OTP button with no verifier, a password screen
with no backend flag, or a terms checkbox that gates nothing.
"""
from __future__ import annotations

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)

checks = 0
failures: list[str] = []


def ok(condition: bool, success: str, failure: str) -> None:
    global checks
    checks += 1
    if not condition:
        failures.append(failure)


def read(path: str) -> str:
    with open(path, encoding="utf-8") as handle:
        return handle.read()


csproj = read("KaamLink.csproj")
supabase_config = read("Core/Api/SupabaseConfig.cs")
email_service = read("Core/Api/SupabaseEmailAuthService.cs")
link_service = read("Core/Api/EmailAuthLinkService.cs")
api_client = read("Core/Api/ApiClient.cs")
api_contracts = read("Core/Api/ApiContracts.cs")
api_http = read("Core/Api/ApiHttp.cs")
maui_program = read("MauiProgram.cs")
app_cs = read("App.xaml.cs")
main_activity = read("Platforms/Android/MainActivity.cs")
login_xaml = read("Features/Auth/LoginPage.xaml")
login_cs = read("Features/Auth/LoginPage.xaml.cs")
login_vm = read("Features/Auth/LoginViewModel.cs")
en_resx = read("Resources/Strings/AppStrings.resx")
ar_resx = read("Resources/Strings/AppStrings.ar.resx")
designer = read("Resources/Strings/AppStrings.Designer.cs")
colors = read("Resources/Styles/Colors.xaml")
dusk = read("Resources/Themes/ClayDusk.xaml")
styles = read("Resources/Styles/Styles.xaml")
appicon = read("Resources/AppIcon/appicon.svg")
appiconfg = read("Resources/AppIcon/appiconfg.svg")
mark = read("Resources/Images/kaamlink_mark.svg")

program = read("backend/src/KaamLink.Api/Program.cs")
startup_guards = read("backend/src/KaamLink.Api/Configuration/StartupGuards.cs")
auth_controller = read("backend/src/KaamLink.Api/Controllers/AuthController.cs")
auth_contracts = read("backend/src/KaamLink.Application/Auth/AuthContracts.cs")
auth_interface = read("backend/src/KaamLink.Application/Auth/IAuthService.cs")
auth_service = read("backend/src/KaamLink.Application/Auth/AuthService.cs")
supabase_options = read("backend/src/KaamLink.Application/Options/SupabaseOptions.cs")
verifier_interface = read("backend/src/KaamLink.Application/Abstractions/ISupabaseUserVerifier.cs")
verifier = read("backend/src/KaamLink.Infrastructure/Identity/SupabaseUserVerifier.cs")
infra_di = read("backend/src/KaamLink.Infrastructure/DependencyInjection.cs")
user = read("backend/src/KaamLink.Domain/Identity/User.cs")
user_config = read("backend/src/KaamLink.Infrastructure/Persistence/Configurations/UserConfiguration.cs")
user_repo_interface = read("backend/src/KaamLink.Application/Abstractions/IUserRepository.cs")
user_repo = read("backend/src/KaamLink.Infrastructure/Identity/UserRepository.cs")
migration = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/20261027000000_Phase06EmailAuth.cs")
migration_designer = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/20261027000000_Phase06EmailAuth.Designer.cs")
snapshot = read("backend/src/KaamLink.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs")
appsettings = read("backend/src/KaamLink.Api/appsettings.json")
env_example = read("backend/.env.example")
compose = read("backend/docker-compose.yml")
fakes = read("backend/tests/KaamLink.UnitTests/Fakes.cs")
exchange_tests = read("backend/tests/KaamLink.UnitTests/Application/AuthServiceSupabaseExchangeTests.cs")
setup_doc = read("docs/email-magic-link-setup.md")
otp_script = read("tools/configure-supabase-otp-templates.py")
all_sh = read("tools/verify-all.sh")

# ------------------------------------------------------------------ client config
ok("KaamLinkSupabaseUrl" in csproj and "KaamLink.SupabaseUrl" in csproj,
   "Supabase URL build metadata present", "csproj: missing KaamLinkSupabaseUrl / KaamLink.SupabaseUrl metadata")
ok("KaamLinkSupabaseAnonKey" in csproj and "KaamLink.SupabaseAnonKey" in csproj,
   "Supabase anon-key build metadata present", "csproj: missing KaamLinkSupabaseAnonKey / KaamLink.SupabaseAnonKey metadata")
ok("sdxywcosqidyopbfevoe.supabase.co" in csproj,
   "default Supabase project URL present", "csproj: default Supabase project URL missing")
for config_path, text in (("appsettings.json", appsettings), (".env.example", env_example),
                          ("docker-compose.yml", compose)):
    ok("sdxywcosqidyopbfevoe.supabase.co" in text,
       f"{config_path} points at the Supabase project", f"{config_path}: Supabase project URL drift")
    ok("service_role" not in text and "sb_secret_" not in text,
       f"{config_path} contains no service-role secret", f"{config_path}: a privileged Supabase key is committed")
ok("service_role" not in csproj and "sb_secret_" not in csproj,
   "client ships only public keys", "csproj: a service-role key is baked into the mobile app")

ok('RedirectUri = "kaamlink://auth-callback"' in supabase_config,
   "legacy deep-link redirect URI remains allowlisted", "SupabaseConfig: redirect URI drift")
for path, name in (("VerifyPath", "auth/v1/verify"), ("UserPath", "auth/v1/user"),
                   ("TokenPath", "auth/v1/token?grant_type=password")):
    ok(path in supabase_config and name in supabase_config,
       f"SupabaseConfig exposes {path}", f"SupabaseConfig: {path} missing")
ok("Uri.UnescapeDataString" in link_service and "TrimStart('#')" in link_service,
   "legacy callback parser decodes the URL fragment", "EmailAuthLinkService: fragment tokens are not decoded")
ok('DataScheme = "kaamlink"' in main_activity and 'DataHost = "auth-callback"' in main_activity
   and "Intent.CategoryBrowsable" in main_activity,
   "Android activity declares the kaamlink callback", "MainActivity: deep-link intent-filter drift")
ok("Exported = true" in main_activity and "LaunchMode.SingleTop" in main_activity,
   "Android activity is exported and single-top for deep links", "MainActivity: exported/single-top deep-link setup missing")
ok(".ConfigureLifecycleEvents(lifecycle =>" in maui_program
   and "lifecycle.AddAndroid" in maui_program
   and "SendOnAppLinkRequestReceived" in maui_program
   and "OnNewIntent" in maui_program and "OnCreate" in maui_program,
   "Android OnCreate/OnNewIntent intents are forwarded to App", "MauiProgram: custom-scheme deep-link forwarding missing")
ok("protected override void OnAppLinkRequestReceived(Uri uri)" in app_cs
   and "base.OnAppLinkRequestReceived(uri)" in app_cs
   and "EmailAuthLinkParser.IsCallback" in app_cs,
   "App receives and validates legacy deep links", "App.xaml.cs: deep-link callback is not handled")
ok("IEmailAuthLinkService" in maui_program and "ISupabaseEmailAuthService" in maui_program,
   "email-auth services are registered", "MauiProgram: email-auth services not registered")
ok("ApiHttp.ConfigureSupabase" in maui_program and "ConfigureSupabase" in api_http,
   "Supabase client shares the bounded HTTP policy", "Supabase HttpClient has no bounded timeout/config")
ok("TimeSpan.FromSeconds(ApiConfig.TimeoutSeconds)" in api_http,
   "Supabase client uses the shared timeout", "Supabase HttpClient can hang on the default 100s timeout")

# ------------------------------------------------------------------ client OTP flow
for dto, marker in (
    ("SupabaseOtpRequestDto", "create_user"),
    ("SupabaseVerifyOtpRequestDto", '"token"'),
    ("SupabasePasswordLoginRequestDto", '"password"'),
    ("SupabaseUpdateUserRequestDto", '"password"'),
    ("SupabaseUserMetadataDto", "password_set"),
    ("SupabaseSessionResponseDto", "access_token"),
):
    ok(dto in api_contracts and marker in api_contracts,
       f"{dto} is declared", f"ApiContracts: {dto} missing")
    ok(f"[JsonSerializable(typeof({dto}))]" in api_contracts,
       f"{dto} is source-gen registered", f"ApiContracts: {dto} registration missing")
ok('JsonPropertyName("type")' in api_contracts and '"email"' in api_contracts,
   "OTP verify body uses GoTrue field names", "ApiContracts: OTP verify body drift")
ok("bool RequiresPasswordSetup = false" in api_contracts,
   "client token response carries password-setup flag", "ApiContracts: RequiresPasswordSetup missing")
ok("ExchangeSupabaseAsync" in api_client.split("public sealed class ApiClient")[0]
   and "ExchangeSupabaseAsync" in api_client.split("public sealed class ApiClient")[1],
   "API client interface + implementation expose the exchange", "ApiClient: exchange method parity broken")
ok('auth/email/exchange' in api_client,
   "exchange endpoint is the backend contract", "ApiClient: wrong exchange endpoint")
for code in ("SupabaseOtpInvalid", "SupabasePasswordSetFailed", "SupabaseInvalidCredentials"):
    ok(f'public const string {code}' in api_client,
       f"ApiClient.Codes.{code} exists", f"ApiClient: {code} missing")

for method in ("SendOtpAsync", "VerifyOtpAsync", "SetPasswordAsync", "SignInWithPasswordAsync"):
    ok(f"public Task" in email_service and method in email_service,
       f"email auth service exposes {method}", f"SupabaseEmailAuthService: {method} missing")
ok("SupabaseConfig.OtpUri" in email_service and "SupabaseConfig.VerifyUri" in email_service
   and "SupabaseConfig.UserUri" in email_service and "SupabaseConfig.TokenUri" in email_service,
   "email auth service targets the correct GoTrue endpoints", "SupabaseEmailAuthService: endpoint drift")
ok("apikey" in email_service and "AuthenticationHeaderValue" in email_service
   and '"Bearer"' in email_service,
   "email auth service sends public/bearer headers", "SupabaseEmailAuthService: GoTrue headers drift")
ok("_network.HasUsableNetwork()" in email_service,
   "email auth service fails fast offline", "SupabaseEmailAuthService: no offline preflight")

# ------------------------------------------------------------------ login UI + VM
for command in ("SendEmailCodeCommand", "VerifyEmailCodeCommand", "SignInWithPasswordCommand",
                "SetPasswordCommand", "SkipPasswordCommand"):
    ok(f'Command="{{Binding {command}}}' in login_xaml,
       f"LoginPage binds {command}", f"LoginPage.xaml: {command} not bound")
for binding in ("IsEmailCodePending", "IsPasswordSetupPending", "EmailCode",
                "Password", "ConfirmPassword", "TermsAccepted", "HasInfo"):
    ok(f"{{Binding {binding}" in login_xaml,
       f"LoginPage binds {binding}", f"LoginPage.xaml: {binding} not bound")
ok('IsChecked="{Binding TermsAccepted}"' in login_xaml,
   "terms checkbox is tickable", "LoginPage.xaml: terms checkbox missing")
ok('Text="{x:Static strings:AppStrings.LoginContinueGoogle}"' in login_xaml
   and 'IsEnabled="False"' in login_xaml and "LoginGoogleSetupNote" in login_xaml,
   "Google remains honestly disabled", "LoginPage.xaml: Google was enabled without OAuth backend")
ok("HandleEmailAuthCallbackAsync" in login_cs,
   "login page exposes a callback handler", "LoginPage.xaml.cs: callback handler missing")
for method in ("SendEmailCodeAsync", "VerifyEmailCodeAsync", "SignInWithPasswordAsync",
               "SetPasswordAsync", "SkipPasswordAsync", "CompleteSupabaseLoginAsync"):
    ok(method in login_vm,
       f"LoginViewModel implements {method}", f"LoginViewModel: {method} missing")
ok(login_vm.count("if (!TermsAccepted)") >= 5,
   "terms gate protects OTP, password, callback and phone paths", "LoginViewModel: terms gate does not protect all auth actions")
ok("RequiresPasswordSetup" in login_vm and "_emailAuth.SetPasswordAsync" in login_vm,
   "password setup follows the backend flag", "LoginViewModel: password setup flow missing")
ok("DigitText.Normalize" in login_vm and "char.IsAsciiDigit" in login_vm,
   "OTP code is normalized and ASCII-validated", "LoginViewModel: OTP input validation drift")
ok("Preferences.Default.Set(TermsAcceptedKey" in login_vm and "Preferences.Default.Get(TermsAcceptedKey" in login_vm,
   "terms acceptance survives app switches", "LoginViewModel: terms state is not persisted")
for key in ("LoginEmailOtpLabel", "LoginEmailOtpPlaceholder", "LoginEmailVerifyCode",
            "LoginEmailOtpInvalid", "LoginPasswordLabel", "LoginPasswordPlaceholder",
            "LoginPasswordSetTitle", "LoginPasswordSetHint", "LoginPasswordConfirmLabel",
            "LoginPasswordConfirmPlaceholder", "LoginPasswordSetButton", "LoginPasswordSkip",
            "LoginPasswordSignIn", "LoginPasswordInvalid", "LoginPasswordMismatch",
            "LoginPasswordInvalidCredentials", "LoginPasswordSetFailed"):
    ok(f'<data name="{key}"' in en_resx and f'<data name="{key}"' in ar_resx
       and f"public static string {key} =>" in designer,
       f"localized {key}", f"localization: {key} missing from EN/AR/Designer")
ok("code" in en_resx and "OAuth client" in en_resx,
   "Google note and email CTA now describe OTP", "strings: link-era copy remains")

# ------------------------------------------------------------------ backend exchange
ok("SupabaseExchangeRequest" in auth_contracts and "AccessToken" in auth_contracts,
   "backend exchange DTO exists", "AuthContracts: SupabaseExchangeRequest missing")
ok("bool RequiresPasswordSetup = false" in auth_contracts,
   "backend token response carries password-setup flag", "AuthContracts: RequiresPasswordSetup missing")
ok("ExchangeSupabaseTokenAsync" in auth_interface and "ExchangeSupabaseTokenAsync" in auth_service,
   "backend auth service exposes the exchange", "IAuthService/AuthService: exchange method drift")
ok('HttpPost("email/exchange")' in auth_controller and "EnableRateLimiting" in auth_controller,
   "backend exchange endpoint is rate-limited", "AuthController: exchange endpoint/rate limit missing")
ok("supabaseVerifier.VerifyAsync" in auth_service and "FindByEmailAsync" in auth_service
   and "RegisterFromEmail" in auth_service and "SyntheticPhone" in auth_service,
   "backend verifies, maps and registers email identities", "AuthService: email exchange is incomplete")
ok("identity.PasswordSet" in auth_service and "RequiresPasswordSetup = !identity.PasswordSet" in auth_service,
   "backend derives password-setup need from Supabase metadata", "AuthService: PasswordSet flag not used")
ok("unitOfWork.SaveChangesAsync" in auth_service,
   "backend persists the new email user/token", "AuthService: exchange does not save changes")
ok("SupabaseOptions" in program and "AddSingleton(supabaseOptions)" in program
   and "ValidateOptions(jwtOptions, otpOptions, supabaseOptions)" in program,
   "Supabase options are bound and validated at startup", "Program.cs: Supabase options wiring missing")
ok("Supabase:Url (HTTPS) and Supabase:AnonKey are required in Production" in startup_guards,
   "production startup requires Supabase config", "StartupGuards: production Supabase guard missing")
ok("SupabaseIdentity" in verifier_interface and "PasswordSet" in verifier_interface,
   "verification port carries password metadata", "ISupabaseUserVerifier: PasswordSet missing")
ok("auth/v1/user" in verifier and "apikey" in verifier
   and "email_confirmed_at" in verifier and "user_metadata" in verifier and "password_set" in verifier,
   "GoTrue /user verification checks confirmed email + password metadata", "SupabaseUserVerifier: token validation drift")
ok("ISupabaseUserVerifier, SupabaseUserVerifier" in infra_di and "Timeout = TimeSpan.FromSeconds(10)" in infra_di,
   "verifier is registered with a bounded client", "DependencyInjection: verifier registration/timeout missing")
ok("SectionName = \"Supabase\"" in supabase_options and "IsConfigured" in supabase_options,
   "Supabase options are a validated config object", "SupabaseOptions: config contract missing")

# ------------------------------------------------------------------ identity schema
ok("string? Email" in user and "RegisterFromEmail" in user and "Email = null" in user,
   "User owns a nullable confirmed email", "User: Email identity column missing")
ok('HasColumnName("email")' in user_config and "ux_users_email" in user_config,
   "EF configuration maps users.email", "UserConfiguration: email mapping missing")
ok("FindByEmailAsync" in user_repo_interface and "FindByEmailAsync" in user_repo,
   "repository can find an email identity", "IUserRepository/UserRepository: FindByEmailAsync missing")
ok('name: "email"' in migration and "ux_users_email" in migration
   and 'b.Property<string>("Email")' in snapshot and 'b.Property<string>("Email")' in migration_designer,
   "migration, snapshot and designer add users.email", "Phase06 migration/snapshot/designer drift")
ok("ux_users_email" in snapshot and "ux_users_email" in migration_designer,
   "unique email index is in the model", "Phase06: unique email index missing")
ok("SupabaseVerifier" in fakes and "StubSupabaseUserVerifier" in fakes
   and "PasswordSet" in fakes and "ExchangeSupabaseTokenAsync" in exchange_tests,
   "unit-test harness covers password metadata", "tests: PasswordSet wiring not covered")
ok("SUPABASE_URL" in env_example and "Supabase__Url" in compose
   and "\"Supabase\"" in appsettings,
   "local and deployed backend receive Supabase config", "Supabase config is missing from one environment")

# ------------------------------------------------------------------ hosted templates
ok('os.environ.get("SUPABASE_ACCESS_TOKEN"' in otp_script
   and '"mailer_templates_magic_link_content":' in otp_script
   and '"mailer_templates_confirmation_content":' in otp_script
   and "{{ .Token }}" in otp_script,
   "Management API script changes both OTP templates", "tools/configure-supabase-otp-templates.py incomplete")
ok("mailer_otp_length" in otp_script and "password_min_length" in otp_script,
   "Management API script sets OTP length and password minimum", "OTP script: config fields missing")
ok("SUPABASE_ACCESS_TOKEN" in setup_doc and "configure-supabase-otp-templates.py" in setup_doc
   and "custom SMTP" in setup_doc and "service-role" in setup_doc,
   "setup doc records PAT, script, SMTP and key safety", "docs/email-magic-link-setup.md incomplete")

# ------------------------------------------------------------------ aurora theme
ok("#4B3FD6" in colors and "#5DE3D4" in colors and "#EBE9FF" in colors,
   "light Aurora Clay palette is indigo/violet + mint", "Colors.xaml: light aurora palette drift")
ok("#B9A9FF" in dusk and "#140E2A" in dusk and "#6FE7D2" in dusk,
   "dusk Aurora palette is rich dark violet, not black", "ClayDusk.xaml: dusk aurora palette drift")
ok('Property="CornerRadius" Value="24"' in styles and 'CornerRadius="28"' in styles,
   "styles use the latest rounded aurora shapes", "Styles.xaml: rounded-shape upgrade missing")
ok("#6FE7D2" in appicon and "#6FE7D2" in appiconfg and "#6FE7D2" in mark
   and "#8B7BFF" in appicon and "#4B3FD6" in mark,
   "app icon assets share the aurora accents", "brand assets no longer share the palette")
ok("verify-email-auth.py" in all_sh,
   "email-auth gate is part of verify-all", "verify-all.sh: new gate is not run")

print(f"{checks} checks, {len(failures)} failures")
if failures:
    print("FAILURES:")
    for failure in sorted(set(failures)):
        print("  -", failure)
    sys.exit(1)
print("EMAIL OTP, PASSWORD, TERMS AND AURORA GATES PASSED")
