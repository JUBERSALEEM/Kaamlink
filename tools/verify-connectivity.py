#!/usr/bin/env python3
"""Static gate for the connectivity, theme, icon and performance hardening.

This is deliberately a source-level gate: the Android/.NET toolchain is not
available in CI here, so the cross-file facts that cause "the app has no
internet" or a half-themed screen must be asserted directly.

Usage:  python3 tools/verify-connectivity.py
Exit:   0 = every check passed; 1 = at least one regression.
"""
from __future__ import annotations

import os
import pathlib
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

checks = 0
failures: list[str] = []


def read(path: str) -> str:
    with open(os.path.join(ROOT, path), encoding="utf-8") as handle:
        return handle.read()


def ok(condition: bool, success: str, failure: str) -> None:
    global checks
    checks += 1
    if condition:
        print(f"  ok   {success}")
    else:
        print(f"  FAIL {failure}")
        failures.append(failure)


def section(title: str) -> None:
    print(f"\n== {title}")


section("Release API configuration")
api_config = read("Core/Api/ApiConfig.cs")
project = read("KaamLink.csproj")
ok(".invalid" not in api_config and "replace-me-when" not in api_config,
   "no reserved placeholder host is shipped in ApiConfig",
   "ApiConfig still contains an unaddressable placeholder host")
ok("public static bool IsConfigured" in api_config,
   "ApiConfig exposes IsConfigured",
   "ApiConfig lost IsConfigured")
ok("public static string ConfigurationSource" in api_config,
   "ApiConfig records where its URL came from",
   "ApiConfig lost ConfigurationSource")
ok("public const int TimeoutSeconds = 15" in api_config,
   "15-second network timeout is declared",
   "network timeout missing from ApiConfig")
ok("AssemblyMetadataAttribute" in project and "KaamLink.ApiBaseUrl" in project,
   "configured URL is embedded in assembly metadata",
   "csproj does not embed KaamLink.ApiBaseUrl")
ok("WarnUnconfiguredKaamLinkApi" in project,
   "Release build warns when the API URL is absent",
   "csproj lost the unconfigured-Release warning")
ok("KaamLinkApiBaseUrl" in project,
   "KaamLinkApiBaseUrl build property exists",
   "csproj lost KaamLinkApiBaseUrl")

section("Shared transport hardening")
api_http = read("Core/Api/ApiHttp.cs")
maui_program = read("MauiProgram.cs")
api_client = read("Core/Api/ApiClient.cs")
session = read("Core/Api/SessionService.cs")
api_exception = read("Core/Api/ApiException.cs")
network_status = read("Core/Api/NetworkStatus.cs")
ok("client.Timeout = TimeSpan.FromSeconds(ApiConfig.TimeoutSeconds)" in api_http,
   "every API HttpClient gets the 15-second timeout",
   "ApiHttp.Configure no longer sets HttpClient.Timeout")
ok("client.BaseAddress = ApiConfig.BaseUri" in api_http,
   "every API HttpClient uses the single BaseUri",
   "ApiHttp.Configure no longer centralizes BaseAddress")
ok(maui_program.count("ApiHttp.Configure") >= 2,
   "typed API client and session auth client share ApiHttp.Configure",
   "one HttpClient is configured separately from the other")
ok("INetworkStatus, NetworkStatus" in maui_program,
   "network status is registered in DI",
   "INetworkStatus is not registered")
ok("ApiConfig.IsConfigured" in api_client and "_network.HasUsableNetwork()" in api_client,
   "ApiClient fails fast on unconfigured/offline states",
   "ApiClient lost its pre-flight configuration/network checks")
ok("IHttpClientFactory" in session and "static readonly HttpClient" not in session,
   "SessionService uses IHttpClientFactory instead of a static client",
   "SessionService still owns a static HttpClient with a frozen BaseAddress")
ok("private bool CanReachApi => ApiConfig.IsConfigured && _network.HasUsableNetwork();" in session
   and session.count("if (!CanReachApi)") >= 2,
   "session restore/refresh skip unconfigured or offline states",
   "SessionService can still make cold start wait on a dead endpoint")
ok("OperationCanceledException" in session,
   "session refresh/reload/logout tolerate timeouts",
   "SessionService does not handle HttpClient timeout exceptions")
ok("NoInternetCode" in api_exception and "ApiNotConfiguredCode" in api_exception,
   "distinct offline/no-network/misconfigured codes exist",
   "ApiException lost its connectivity codes")
ok("NetworkAccess" in network_status and "HasUsableNetwork" in network_status,
   "MAUI Connectivity is wrapped for the transport layer",
   "NetworkStatus is missing")

section("Android network policy")
release_manifest = read("Platforms/Android/AndroidManifest.xml")
debug_manifest = read("Platforms/Android/AndroidManifest.Debug.xml")
release_config = read("Platforms/Android/Resources/xml/network_security_config.xml")
debug_config = read("Platforms/Android/Resources/xml/network_security_config_debug.xml")
ok('android:networkSecurityConfig="@xml/network_security_config"' in release_manifest,
   "Release manifest keeps the strict policy",
   "Release manifest points away from the strict network policy")
ok(re.search(r'<base-config[^>]*cleartextTrafficPermitted="false"', release_config) is not None,
   "Release cleartext stays OFF by default",
   "Release cleartext policy is too permissive")
ok('android:networkSecurityConfig="@xml/network_security_config_debug"' in debug_manifest,
   "Debug manifest selects the Debug-only policy",
   "Debug manifest does not reference network_security_config_debug")
ok(re.search(r'<base-config[^>]*cleartextTrafficPermitted="true"', debug_config) is not None,
   "Debug LAN cleartext is permitted",
   "Debug LAN testing would be blocked by the network policy")
ok("AndroidManifest.Debug.xml" in project,
   "csproj selects the Debug manifest by configuration",
   "Debug manifest is never selected by the build")

section("Localized connectivity errors")
en = read("Resources/Strings/AppStrings.resx")
ar = read("Resources/Strings/AppStrings.ar.resx")
designer = read("Resources/Strings/AppStrings.Designer.cs")
login_vm = read("Features/Auth/LoginViewModel.cs")
for key in ("ErrorNoInternet", "ErrorNoInternetTitle",
            "ErrorApiNotConfigured", "ErrorApiNotConfiguredTitle"):
    ok(f'<data name="{key}"' in en and f'<data name="{key}"' in ar
       and f"public static string {key} =>" in designer,
       f"{key} exists in EN, AR and Designer",
       f"{key} is missing from one localization surface")
ok("IsNoInternet" in login_vm and "IsMisconfigured" in login_vm,
   "login maps no-network and misconfigured errors",
   "login still shows a generic offline message for configuration errors")

section("Theme, icon and rendering consistency")
light = read("Resources/Styles/Colors.xaml")
dusk = read("Resources/Themes/ClayDusk.xaml")
styles = read("Resources/Styles/Styles.xaml")
theme_service = read("Core/Presentation/Theming/ThemeService.cs")
theme_catalog = read("Core/Presentation/Theming/ThemeCatalog.cs")
clay_material = read("Core/Presentation/Clay/ClayMaterial.cs")
login_page = read("Features/Auth/LoginPage.xaml")
appicon = read("Resources/AppIcon/appicon.svg")
appiconfg = read("Resources/AppIcon/appiconfg.svg")
mark = read("Resources/Images/kaamlink_mark.svg")
hero = read("Resources/Images/login_hero.svg")
for name, text in (("light", light), ("dusk", dusk)):
    ok('<LinearGradientBrush x:Key="ClayPageBrush"' in text
       and '<LinearGradientBrush x:Key="ClayCardBrush"' in text,
       f"{name} palette uses gradient clay surfaces",
       f"{name} palette regressed to flat clay surfaces")
    ok("PageBackgroundTop" in text and "CardSurfaceBottom" in text,
       f"{name} palette keeps contrast-checked gradient stops",
       f"{name} palette lost its gradient stop tokens")
ok('Value="{DynamicResource ClayPageBrush}"' in styles,
   "global page style consumes the gradient page brush",
   "pages no longer use the themed page brush")
ok("theme.CreatePalette()" in theme_service
   and "PaletteMarkerKey" in theme_service
   and "dispatcher.Dispatch(RefreshClayShadows)" in theme_service
   and "!dispatcher.IsDispatchRequired" not in theme_service,
   "theme toggle swaps compiled dictionaries and defers shadow refresh",
   "ThemeService regressed to Source-only dictionaries or synchronous shadow refresh")
ok("CreatePalette" in theme_catalog
   and "ClayPalette" in theme_catalog
   and "ClayDuskPalette" in theme_catalog,
   "ThemeCatalog exposes compiled palette factories",
   "ThemeCatalog no longer provides typed palette factories")
ok("HashSet<IVisualTreeElement>" in clay_material
   and "Stack<" in clay_material
   and "MaxRefreshDepth" in clay_material,
   "clay shadow refresh is iterative, cycle-safe and depth-limited",
   "ClayMaterial.Refresh can recurse without a visited set (stack-overflow crash path)")
ok("SafeInitializeAsync" in read("Features/Auth/LoginPage.xaml.cs")
   and "SafeInitializeAsync" in read("Features/Onboarding/RoleSelectionPage.xaml.cs"),
   "async page initialization is observed and cannot crash as an unobserved task",
   "a page still fire-and-forgets initialization without a catch wrapper")
role_page = read("Features/Onboarding/RoleSelectionPage.xaml")
ok(login_page.count('InputTransparent="False"') >= 2
   and 'InputTransparent="True"' in login_page
   and 'InputTransparent="False"' in role_page,
   "theme/country buttons and country-row gestures have explicit tap routing",
   "a tappable control lost explicit InputTransparent routing (clicks can be swallowed)")
ok('Text="{x:Static strings:AppStrings.LoginContinueGoogle}"' in login_page
   and 'IsEnabled="False"' in login_page
   and "LoginGoogleSetupNote" in login_page,
   "Google sign-in remains visibly disabled with an explanation",
   "Google button was enabled without a server-side identity exchange")
ok("ItemSizingStrategy=\"MeasureFirstItem\"" in login_page,
   "country picker CollectionView uses a fast item-sizing strategy",
   "country picker lost its CollectionView optimization")
ok("linearGradient" in appicon and "linearGradient" in mark and "linearGradient" in hero,
   "app icon, login mark and hero share the rich gradient language",
   "one brand asset regressed to flat colour")
ok("6FE7D2" in appicon and "6FE7D2" in appiconfg and "6FE7D2" in mark
   and "8B7BFF" in appicon and "8B7BFF" in mark
   and "4B3FD6" in appicon and "4B3FD6" in mark,
   "adaptive icon foreground and login mark share the aurora mint/indigo/violet accents",
   "brand mark and app icon use different accent colours")

section("Android resource filenames (Resizetizer)")
# Microsoft.Maui.Resizetizer rejects uppercase letters, hyphens, hidden files and
# names that start/end with a digit for MauiImage/MauiIcon/MauiSplashScreen items.
# `kaamlink-mark.svg` and `login-hero.svg` produced exactly that build error.
valid_stem = re.compile(r"[a-z](?:[a-z0-9_]*[a-z])?")
invalid_assets: list[str] = []
for asset_dir in ("Resources/Images", "Resources/AppIcon", "Resources/Splash"):
    for current, _subdirs, filenames in os.walk(os.path.join(ROOT, asset_dir)):
        for filename in filenames:
            stem = os.path.splitext(filename)[0]
            if not valid_stem.fullmatch(stem):
                invalid_assets.append(os.path.relpath(os.path.join(current, filename), ROOT))
ok(not invalid_assets,
   "all resizetizer image/icon/splash filenames are Android-safe",
   f"invalid Android resource filenames: {invalid_assets}")
ok('MauiImage Include="Resources\\Images\\*"' in project,
   "csproj includes the image directory through the standard wildcard",
   "csproj lost the MauiImage wildcard include")

# Every literal local image Source in XAML must resolve to one of those files.
bad_sources: list[str] = []
for xaml_path in sorted(str(p) for p in
                        pathlib.Path(ROOT).glob("Features/**/*.xaml")):
    rel = os.path.relpath(xaml_path, ROOT)
    for source in re.findall(r'Source="([A-Za-z0-9_.-]+\.(?:svg|png|jpe?g|webp|gif|bmp))"',
                             read(rel)):
        stem = os.path.splitext(source)[0]
        if (not valid_stem.fullmatch(stem)
                or not os.path.isfile(os.path.join(ROOT, "Resources/Images", source))):
            bad_sources.append(f"{rel}: {source}")
ok(not bad_sources,
   "every literal XAML image Source resolves to a real image asset",
   f"XAML references missing image assets: {bad_sources}")

section("Documentation")
doc_path = "docs/connectivity-and-release-configuration.md"
ok(os.path.isfile(os.path.join(ROOT, doc_path)),
   "connectivity/release runbook exists",
   "connectivity/release runbook is missing")
doc = read(doc_path)
for term in ("KaamLinkApiBaseUrl", "HTTPS", "SMS", "adb reverse"):
    ok(term in doc,
       f"runbook documents {term}",
       f"runbook does not document {term}")

print(f"\n{checks} checks, {len(failures)} failures")
if failures:
    print("FAILURES:")
    for failure in failures:
        print("  -", failure)
    sys.exit(1)
print("CONNECTIVITY AND UI HARDENING CHECKS PASSED")
