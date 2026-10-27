#!/usr/bin/env python3
"""
Negative tests for the KaamLink verification gates ("who verifies the verifier").

A gate that passes because it silently stopped looking is worse than no gate at
all - that is exactly how 14 dead command bindings once reported ALL GREEN. This
harness injects one realistic defect at a time, runs the gate that is supposed to
catch it, and requires a non-zero exit. Every mutation is applied to a temporary
copy and always restored, so the working tree is left clean.

Usage:  python3 tools/verify-gates-selftest.py [-v]
Exit:   0 = every gate detected its defect; 1 = a gate missed one (or crashed).
"""

from __future__ import annotations

import os
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# (case name, file, literal old -> new, gate command)
CASES = [
    (
        "app cold start bypasses sign-in",
        "AppShell.xaml",
        ('Route="login"', 'Route="welcome"'),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "misspelled binding path",
        "Features/Worker/WorkerHomePage.xaml",
        ("{Binding CompletenessText}", "{Binding CompletenessTextTYPO}"),
        ["tools/verify-bindings.py"],
    ),
    (
        "misspelled command",
        "Features/Jobs/VacancyApplicationsPage.xaml",
        ("BindingContext.HireCommand", "BindingContext.HireTYPO"),
        ["tools/verify-bindings.py"],
    ),
    (
        "command never bound (dropped feature)",
        "Features/Jobs/VacancyDetailPage.xaml",
        ('Command="{Binding ApplyCommand}"', 'Command="{Binding BackCommand}"'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "hardcoded UI text",
        "Features/Jobs/VacancyFeedPage.xaml",
        ("Title=\"{x:Static strings:AppStrings.FeedNoResultsTitle}\"", 'Title="No results"'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "emoji used as an icon",
        "Features/Worker/WorkerHomePage.xaml",
        ("</ContentPage>", "    <Label Text=\"\U0001F600\" />\n</ContentPage>"),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "text glyph baked into a display string",
        "Features/Worker/WorkerHomeViewModel.cs",
        ("var label = LocalizationHelpers.LocalizedSkill(skill);",
         'var label = "\\u2605 " + LocalizationHelpers.LocalizedSkill(skill);'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "x:Reference inside a DataTemplate",
        "Features/Jobs/MyApplicationsPage.xaml",
        ("{Binding BindingContext.WithdrawCommand, Source={RelativeSource AncestorType={x:Type ContentPage}}}",
         "{Binding BindingContext.WithdrawCommand, Source={x:Reference Page}}"),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "Color token assigned to Brush-typed Stroke",
        "Features/Jobs/VacancyFeedPage.xaml",
        ('Stroke="{DynamicResource BorderBrush}"', 'Stroke="{DynamicResource Border}"'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "converter fed the wrong type",
        "Features/Jobs/CompanyVacanciesPage.xaml",
        ("IsVisible=\"{Binding HasCompanyName, Converter={StaticResource InvertedBoolConverter}}\"",
         "IsVisible=\"{Binding CompanyName, Converter={StaticResource InvertedBoolConverter}}\""),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "undeclared StaticResource key",
        "Features/Jobs/MyApplicationsPage.xaml",
        ("{StaticResource SurfaceCardDense}", "{StaticResource SurfaceCardDenseTYPO}"),
        ["tools/verify-phase03.py"],
    ),
    (
        "missing Arabic translation",
        "Resources/Strings/AppStrings.ar.resx",
        ('<data name="ResendCta" xml:space="preserve">', '<data name="ResendCtaXXX" xml:space="preserve">'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "icon glyph written as a XAML entity",
        "Features/Worker/WorkerHomePage.xaml",
        ("</ContentPage>", "    <Label Text=\"&#x2605;\" />\n</ContentPage>"),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "icon key holding more than one glyph",
        "Resources/Styles/Icons.xaml",
        ('<x:String x:Key="IconStar">', '<x:String x:Key="IconStar">AB'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "escaped route value never decoded on receipt",
        "Features/Auth/OtpViewModel.cs",
        ('Phone = Uri.UnescapeDataString(phoneText);', 'Phone = phoneText;'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "phone input stops normalizing Arabic-Indic digits",
        "Core/Text/PhoneInput.cs",
        ("var cleaned = SeparatorsRegex().Replace(DigitText.Normalize(raw).Trim(), string.Empty);",
         "var cleaned = SeparatorsRegex().Replace(raw.Trim(), string.Empty);"),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "OTP accepts Arabic-Indic digits the API rejects",
        "Features/Auth/OtpViewModel.cs",
        ("var code = DigitText.Normalize(Code).Trim();\n            if (code.Length != 6 || !code.All(char.IsAsciiDigit))",
         "var code = Code.Trim();\n            if (code.Length != 6 || !code.All(char.IsDigit))"),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "tappable element without accessibility semantics",
        "Features/Jobs/VacancyFeedPage.xaml",
        ('SemanticProperties.Description="{Binding Title}">', ">"),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "generated-property partial method has the wrong type",
        "Features/Assessments/AssessmentRunViewModel.cs",
        ("partial void OnIsLoadedChanged(bool value)", "partial void OnIsLoadedChanged(string value)"),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "duplicate x:Name in one page",
        "Features/Assessments/AssessmentRunPage.xaml",
        ("</ContentPage>", '    <Label x:Name="Page" Text="dup" />\n</ContentPage>'),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "CanExecute names a method that does not exist",
        "Features/Assessments/AssessmentRunViewModel.cs",
        ("CanExecute = nameof(CanSubmitAll)", "CanExecute = nameof(CanSubmitAllTYPO)"),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "colour token baked with StaticResource (theme switch would not repaint)",
        "Features/Auth/LoginPage.xaml",
        ('TextColor="{DynamicResource Primary}"', 'TextColor="{StaticResource Primary}"'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "icon key referenced but not declared",
        "Features/Auth/LoginPage.xaml",
        ("{StaticResource IconAlertCircle}", "{StaticResource IconAlertCircleTYPO}"),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "clay elevation dropped from a card style",
        "Resources/Styles/Styles.xaml",
        ('<Setter Property="clay:ClayMaterial.Elevation" Value="Card" />', ""),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "theme palette missing a token",
        "Resources/Themes/ClayDusk.xaml",
        ('    <Color x:Key="AccentSoft">#123E3B</Color>\n', ""),
        ["tools/verify-theme-contrast.py"],
    ),
    (
        "dev host that the Android cleartext policy blocks (the real \"no internet\")",
        "Core/Api/ApiConfig.cs",
        ('"http://localhost:8080/"', '"http://192.168.1.10:8080/"'),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "single-ABI APK (the real \"app not compatible\")",
        "KaamLink.csproj",
        ("<RuntimeIdentifiers>android-arm;android-arm64;android-x86;android-x64</RuntimeIdentifiers>",
         "<RuntimeIdentifiers>android-x64</RuntimeIdentifiers>"),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "INTERNET permission dropped from the manifest",
        "Platforms/Android/AndroidManifest.xml",
        ('\t<uses-permission android:name="android.permission.INTERNET" />\n', ""),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "IList member used on an ICollection framework property (the real CS1501)",
        "Core/Presentation/Theming/ThemeService.cs",
        ("            merged.Add(replacement);",
         "            merged.Insert(1, replacement);"),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "interface member dropped (the real CS1061)",
        "Core/Api/ApiClient.cs",
        ("        Task<StartAssessmentResponseDto> StartAssessmentAsync(Guid skillId, CancellationToken ct = default);\n\n", ""),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "view model calls a method the interface does not declare",
        "Features/Assessments/MyAssessmentsViewModel.cs",
        ("_api.GetMyAssessmentsAsync()", "_api.GetMyAssessmentsTYPO()"),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "missing using directive for a used type",
        "Features/Auth/OtpViewModel.cs",
        ("using KaamLink.Core.Text;\n", ""),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "nonexistent resource key",
        "Features/Auth/LoginViewModel.cs",
        ("AppStrings.ErrorGeneric", "AppStrings.ErrorGenericTYPO"),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "nonexistent API error code",
        "Features/Auth/LoginViewModel.cs",
        ("ApiClient.Codes.RateLimited", "ApiClient.Codes.RateLimitedTYPO"),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "skeleton screen with no x:DataType",
        "Features/Jobs/MyApplicationsPage.xaml",
        ('x:DataType="vm:MyApplicationsViewModel"', "x:DataType=\"{x:Null}\""),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "network timeout removed from shared HttpClient",
        "Core/Api/ApiHttp.cs",
        ("client.Timeout = TimeSpan.FromSeconds(ApiConfig.TimeoutSeconds);",
         "client.Timeout = TimeSpan.FromSeconds(100);"),
        ["tools/verify-connectivity.py"],
    ),
    (
        "pre-flight network check removed from ApiClient",
        "Core/Api/ApiClient.cs",
        ("if (!_network.HasUsableNetwork())", "if (false)"),
        ["tools/verify-connectivity.py"],
    ),
    (
        "Release warning for missing API URL removed",
        "KaamLink.csproj",
        ('Name="WarnUnconfiguredKaamLinkApi"', 'Name="NoWarningTarget"'),
        ["tools/verify-connectivity.py"],
    ),
    (
        "Debug LAN cleartext policy disabled",
        "Platforms/Android/Resources/xml/network_security_config_debug.xml",
        ('<base-config cleartextTrafficPermitted="true">',
         '<base-config cleartextTrafficPermitted="false">'),
        ["tools/verify-connectivity.py"],
    ),
    (
        "theme shadow refresh no longer deferred to the dispatcher",
        "Core/Presentation/Theming/ThemeService.cs",
        ("dispatcher.Dispatch(RefreshClayShadows);", "RefreshClayShadows();"),
        ["tools/verify-connectivity.py"],
    ),
    (
        "runtime theme switch drops compiled palette factory",
        "Core/Presentation/Theming/ThemeService.cs",
        ("var replacement = theme.CreatePalette();", "var replacement = new ResourceDictionary();"),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "clay shadow refresh loses its cycle guard",
        "Core/Presentation/Clay/ClayMaterial.cs",
        ("var visited = new HashSet<IVisualTreeElement>(ReferenceEqualityComparer.Instance);", ""),
        ["tools/verify-connectivity.py"],
    ),
    (
        "light clay page brush regressed to a flat fill",
        "Resources/Styles/Colors.xaml",
        ('<LinearGradientBrush x:Key="ClayPageBrush"',
         '<SolidColorBrush x:Key="ClayPageBrush"'),
        ["tools/verify-connectivity.py"],
    ),
    (
        "dusk clay card brush regressed to a flat fill",
        "Resources/Themes/ClayDusk.xaml",
        ('<LinearGradientBrush x:Key="ClayCardBrush"',
         '<SolidColorBrush x:Key="ClayCardBrush"'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "session cold start skips the endpoint/network pre-flight",
        "Core/Api/SessionService.cs",
        ("private bool CanReachApi => ApiConfig.IsConfigured && _network.HasUsableNetwork();",
         "private bool CanReachApi => true;"),
        ["tools/verify-connectivity.py"],
    ),
    (
        "hyphenated Android image filename reintroduced",
        "Features/Auth/LoginPage.xaml",
        ('Source="kaamlink_mark.svg"', 'Source="kaamlink-mark.svg"'),
        ["tools/verify-connectivity.py"],
    ),
    (
        "JsonSerializerDefaults used without System.Text.Json import",
        "Core/Text/CountryPhoneDirectoryJsonContext.cs",
        ("using System.Text.Json;\n", ""),
        ["tools/verify-global-phone-directory.py"],
    ),
    (
        "framework STJ using gap in API contracts (CS0103)",
        "Core/Api/ApiContracts.cs",
        ("using System.Text.Json;\n", ""),
        ["tools/verify-compile-surface.py"],
    ),
    (
        "Button style uses Label-only HorizontalTextAlignment",
        "Resources/Styles/Styles.xaml",
        ("    <Style TargetType=\"Button\" x:Key=\"SecondaryButton\">\n",
         "    <Style TargetType=\"Button\" x:Key=\"SecondaryButton\">\n"
         "        <Setter Property=\"HorizontalTextAlignment\" Value=\"Start\" />\n"),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "primary button loses its gradient Background",
        "Resources/Styles/Styles.xaml",
        ('<Setter Property="Background" Value="{DynamicResource ClayPrimaryBrush}" />',
         '<Setter Property="BackgroundColor" Value="{DynamicResource PrimaryFill}" />'),
        ["tools/verify-ui-redesign.py"],
    ),
    (
        "country row label swallows tap gestures",
        "Features/Auth/LoginPage.xaml",
        ('InputTransparent="True"', 'InputTransparent="False"'),
        ["tools/verify-connectivity.py"],
    ),
    (
        "Google button enabled without a backend",
        "Features/Auth/LoginPage.xaml",
        ('IsEnabled="False"', 'IsEnabled="True"'),
        ["tools/verify-connectivity.py"],
    ),
    (
        "magic-link redirect URI drift",
        "Core/Api/SupabaseConfig.cs",
        ('RedirectUri = "kaamlink://auth-callback"', 'RedirectUri = "kaamlink://wrong"'),
        ["tools/verify-email-auth.py"],
    ),
    (
        "terms checkbox no longer gates sign-in",
        "Features/Auth/LoginViewModel.cs",
        ("if (!TermsAccepted)", "if (false)"),
        ["tools/verify-email-auth.py"],
    ),
    (
        "backend exchange skips Supabase verification",
        "backend/src/KaamLink.Application/Auth/AuthService.cs",
        ("var identity = await supabaseVerifier.VerifyAsync(request.AccessToken, ct);",
         'var identity = new SupabaseIdentity("spoofed", "attacker@example.com");'),
        ["tools/verify-email-auth.py"],
    ),
    (
        "service-role key committed to the client",
        "KaamLink.csproj",
        ("sb_publishable_FV1pX1Xo3QCxCabFetDhIg_RszBDMab", "sb_secret_SHOULD_NOT_SHIP"),
        ["tools/verify-email-auth.py"],
    ),
    (
        "magic-link deep link is no longer handled",
        "App.xaml.cs",
        ("OnAppLinkRequestReceived", "OnAppLinkRequestReceivedTYPO"),
        ["tools/verify-email-auth.py"],
    ),
    (
        "Android deep-link intent is not forwarded",
        "MauiProgram.cs",
        ("ConfigureLifecycleEvents", "ConfigureLifecycleEventsTYPO"),
        ["tools/verify-email-auth.py"],
    ),
    (
        "email OTP command no longer bound",
        "Features/Auth/LoginPage.xaml",
        ("SendEmailCodeCommand", "SendEmailCodeCommandTYPO"),
        ["tools/verify-email-auth.py"],
    ),
    (
        "password setup ignores the backend flag",
        "backend/src/KaamLink.Application/Auth/AuthService.cs",
        ("RequiresPasswordSetup = !identity.PasswordSet", "RequiresPasswordSetup = false"),
        ["tools/verify-email-auth.py"],
    ),
    (
        "OTP template script drops confirm-signup template",
        "tools/configure-supabase-otp-templates.py",
        ("mailer_templates_confirmation_content", "mailer_templates_confirmation_contentTYPO"),
        ["tools/verify-email-auth.py"],
    ),
    (
        "Management API token missing from OTP script",
        "tools/configure-supabase-otp-templates.py",
        ('os.environ.get("SUPABASE_ACCESS_TOKEN"', 'os.environ.get("SUPABASE_MANAGEMENT_TOKEN_TYPO"'),
        ["tools/verify-email-auth.py"],
    ),
    (
        "helper Edge Function loses admin-token protection",
        "backend/supabase/functions/configure-otp-templates/index.ts",
        ('Deno.env.get("OTP_TEMPLATE_ADMIN_TOKEN")', 'Deno.env.get("NO_ADMIN_TOKEN")'),
        ["tools/verify-email-auth.py"],
    ),
    (
        "helper Edge Function targets the wrong project",
        "backend/supabase/functions/configure-otp-templates/index.ts",
        ('PROJECT_REF = "zyklrndmmcmsktagkzek"', 'PROJECT_REF = "wrong-project-ref"'),
        ["tools/verify-email-auth.py"],
    ),
]


def run(command: list[str]) -> tuple[int, str]:
    result = subprocess.run(
        [sys.executable] + command, cwd=ROOT, capture_output=True, text=True)
    return result.returncode, (result.stdout + result.stderr).strip()


def main() -> int:
    verbose = "-v" in sys.argv
    failures: list[str] = []
    print(f"negative tests: {len(CASES)}\n")

    for name, rel_path, (old, new), gate in CASES:
        path = os.path.join(ROOT, rel_path)
        with open(path, encoding="utf-8") as handle:
            original = handle.read()

        if old not in original:
            failures.append(f"{name}: mutation anchor not found in {rel_path} "
                            f"(the gate may be testing dead text)")
            print(f"  SKIP/FAIL  {name:<44} anchor missing")
            continue

        backup = tempfile.NamedTemporaryFile(
            "w", suffix=".bak", delete=False, encoding="utf-8")
        try:
            backup.write(original)
            backup.close()

            with open(path, "w", encoding="utf-8") as handle:
                handle.write(original.replace(old, new, 1))

            code, output = run(gate)
            detected = code != 0
            print(f"  {'DETECTED ' if detected else 'MISSED   '} {name:<44} "
                  f"[{os.path.basename(gate[0])} exit={code}]")
            if verbose or not detected:
                for line in output.splitlines()[-4:]:
                    print(f"              {line}")
            if not detected:
                failures.append(f"{name}: {gate[0]} passed a broken file")

            # the gate must also be green again once the file is restored
            with open(path, "w", encoding="utf-8") as handle:
                handle.write(original)
            restore_code, _ = run(gate)
            if restore_code != 0:
                failures.append(f"{name}: gate still red after restoring {rel_path}")
                print(f"  REGRESSION {rel_path} did not return to green after restore")
        finally:
            os.unlink(backup.name)
            with open(path, "w", encoding="utf-8") as handle:
                handle.write(original)

    print()
    if failures:
        print(f"{len(failures)} gate self-test failure(s):")
        for failure in failures:
            print("  -", failure)
        return 1
    print(f"all {len(CASES)} injected defects were detected; tree restored clean")
    return 0


if __name__ == "__main__":
    sys.exit(main())
