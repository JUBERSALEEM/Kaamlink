# Connectivity diagnosis and Release configuration

_Last reviewed: 2026-10-27 (branch `arena/b0f64897-kaamlink`)._

This is the current source of truth for "why does the app have no internet?" and
"how do I build an APK that can actually sign in?". It replaces the older
placeholder-URL instructions in `docs/first-boot-guide.md`.

## Short answer

The Android **permissions were never the bug**: `INTERNET` and
`ACCESS_NETWORK_STATE` are present, and the local dev hosts are allowlisted. The
break was configuration and deployment:

1. **Release shipped a reserved, unaddressable placeholder host.** Every DNS
   lookup failed before a packet left the phone. The client now reads a real
   HTTPS URL from build metadata and shows a clear "Server not configured"
   state instead of blaming the user's connection.
2. **Debug could only reach a developer machine** through `10.0.2.2` (emulator)
   or `localhost` + `adb reverse` (USB). On mobile data, another Wi-Fi, or with
   the reverse tunnel stopped, neither address exists. Debug now also accepts a
   LAN URL through `KaamLinkApiBaseUrl`, and a Debug-only Android manifest
   permits cleartext for QA. Release keeps TLS-only.
3. **No request timeout and a single frozen auth HttpClient** made a dead host
   look like a frozen app. The KaamLink API client, session client and Supabase
   GoTrue client now share the 15-second timeout policy and a pre-flight
   network/configuration check.
4. **End-to-end login still needs a hosted backend.** The backend is present in
   this repository but is not deployed to a public HTTPS origin. Phone OTP also
   needs a production SMS provider; email magic link needs the Supabase config
   and, for production volume, custom SMTP. Those are infrastructure/provider
   prerequisites, not client bugs.

## What changed in the app

| Area | Before | Now |
|---|---|---|
| Release endpoint | Hardcoded reserved `.invalid` host | `-p:KaamLinkApiBaseUrl=https://api.example.com/` baked into `AssemblyMetadata`; unconfigured builds warn and show a localized setup error |
| Debug endpoint | Emulator/USB loopback only | Same loopback defaults, plus a Debug-only Preferences override or any LAN URL passed to the build property |
| Supabase auth | No client auth provider | Public URL + publishable/anon key baked in at build time; `-p:KaamLinkSupabaseUrl=...` / `-p:KaamLinkSupabaseAnonKey=...` override them |
| Magic-link callback | No deep link | `kaamlink://auth-callback` Android intent filter, fragment-token parser, and backend exchange endpoint |
| Cleartext policy | One strict policy for all builds | Strict `network_security_config.xml` for Release; `network_security_config_debug.xml` + `AndroidManifest.Debug.xml` for Debug only |
| Transport | Typed client from `IHttpClientFactory`; static auth client from a second code path; default 100 s timeout | API/session clients use `ApiHttp.Configure`; Supabase uses `ApiHttp.ConfigureSupabase`; all use the shared 15 s timeout and JSON accept header |
| Failure states | "Offline" for DNS failure, timeout, airplane mode and missing config | Distinct `client.no_internet`, `client.api_not_configured`, Supabase not-configured / rate-limit / invalid-link and offline codes with localized copy |
| Diagnostics | Base address only in Debug log | Base address, configuration source, network state and device type in Debug transport logs |
| Theme/shadows | Dispatcher condition inverted on background threads | Correct `IsDispatchRequired` branch; compiled palettes, deferred dispatcher refresh, iterative cycle-safe shadow traversal |
| Lists | Default `CollectionView` measurement | `ItemSizingStrategy="MeasureFirstItem"` + `KeepScrollOffset` on the country picker and job lists |
| Android image resources | `kaamlink-mark.svg` / `login-hero.svg` contained hyphens and failed `Microsoft.Maui.Resizetizer` | Renamed to `kaamlink_mark.svg` / `login_hero.svg`; all XAML references, docs and the connectivity gate updated |
| Source-generated JSON | `CountryPhoneDirectoryJsonContext.cs` used `JsonSerializerDefaults.Web` without importing `System.Text.Json` (CS0103 in a real .NET build) | Added `using System.Text.Json;`; compile-surface and phone-directory gates assert the STJ namespace imports |
| Country picker rows | `CountryListButton` applied `HorizontalTextAlignment` to a MAUI `Button` (XLS0431 / XC0001) | Rebuilt as `Border` + `Label` + `TapGestureRecognizer`; the redesign gate rejects text-alignment setters on Button styles/elements |
| Theme switch / crash hardening | A C# `ResourceDictionary { Source = ... }` does not load MAUI XAML, so Day/Dusk clicks appeared dead; recursive clay-shadow refresh could cycle | Compiled `ClayPalette` / `ClayDuskPalette` dictionaries, deferred dispatcher refresh, iterative cycle-safe shadow traversal, and observed async page init |
| Rich color | Dusk was a near-black charcoal; buttons were solid | Aurora indigo/violet + cyan/mint palette, rich midnight-violet dusk (not black), gradient `Button.Background` on Android, rounder shapes, and updated icon/hero/splash SVGs |

## Build commands

### Release / staging (required for real login)

The API URL must be **public HTTPS**, include the trailing slash, and point at
the KaamLink API root (the app appends `api/v1/...`). Do not use a LAN IP here.

```bash
dotnet build KaamLink.csproj -c Release -f net10.0-android \
  -p:KaamLinkStandaloneApk=true \
  -p:KaamLinkApiBaseUrl=https://api.example.com/
```

Supabase settings have safe public defaults. Override them when moving to a
different project:

```bash
dotnet build KaamLink.csproj -c Release -f net10.0-android \
  -p:KaamLinkStandaloneApk=true \
  -p:KaamLinkApiBaseUrl=https://api.example.com/ \
  -p:KaamLinkSupabaseUrl=https://your-project-ref.supabase.co \
  -p:KaamLinkSupabaseAnonKey=sb_publishable_your_key
```

Never pass a service-role key to the mobile build. `SupabaseConfig` rejects a key
containing `service_role`, and the email-auth gate checks the committed config.

If `KaamLinkApiBaseUrl` is omitted, the build emits a warning and the APK
installs but the login screen reports **"Server not configured"** instead of
pretending the device is offline. That is deliberate: a silently unaddressable
Release APK is what caused the original confusion.

### Debug on the Android emulator

```bash
dotnet run --project KaamLink.csproj -f net10.0-android
# Emulator uses http://10.0.2.2:8080/ automatically.
```

### Debug on a physical phone, USB

```bash
adb reverse tcp:8080 tcp:8080
dotnet run --project KaamLink.csproj -f net10.0-android
# Physical device uses http://localhost:8080/ automatically.
```

`adb reverse` is per-connection: re-run it after unplugging/replugging the cable
or restarting adb.

### Debug on a physical phone, same Wi-Fi (no cable)

Find the host machine's LAN IP (for example `192.168.1.20`) and build Debug with:

```bash
dotnet build KaamLink.csproj -c Debug -f net10.0-android \
  -p:KaamLinkStandaloneApk=true \
  -p:KaamLinkApiBaseUrl=http://192.168.1.20:8080/
```

The Debug-only network policy allows this cleartext host. Release rejects any
non-loopback `http://` URL. The backend must listen on `0.0.0.0:8080`, not only
`127.0.0.1:8080`.

For a one-off QA override without rebuilding, set the `kaamlink.api.baseUrl`
string in MAUI Preferences to an absolute URL with a trailing slash (Debug
builds only).

## Backend prerequisites (still required for production auth)

Connectivity is only the first hop. For a real user to complete login:

1. Deploy `backend/` to a publicly reachable **HTTPS** origin.
2. Apply migration `20261027000000_Phase06EmailAuth` (`users.email` +
   `ux_users_email`). Startup migration application is configurable.
3. Configure the API's database and JWT/refresh-token settings.
4. Set `Supabase__Url` (HTTPS) and `Supabase__AnonKey` in the deployed API.
   Production startup guards reject a missing or half-configured Supabase
   section. See [`email-magic-link-setup.md`](email-magic-link-setup.md).
5. Configure custom SMTP in Supabase before real magic-link traffic; the
   built-in email provider is rate-limited.
6. Replace development `Otp:Provider=log` with a production **SMS** provider if
   phone OTP is offered, and store its credentials as secrets/environment
   variables — never in the mobile app or this repository.
7. Keep Google disabled until server-side ID-token validation and account
   linking exist (see `docs/auth-and-phone-login-decision.md`).
8. Verify `GET /health/ready`, then `POST api/v1/auth/otp/request` /
   `api/v1/auth/otp/verify` **and** `POST api/v1/auth/email/exchange` from a real
   phone on mobile data.

A connected Supabase project is not a deployed KaamLink API. No public KaamLink
backend URL or SMS credentials were supplied, so this change cannot truthfully
claim end-to-end production login is working. It removes the client-side causes,
makes the remaining deployment step explicit, and prevents another silently
broken Release build.

## Verification run

```bash
bash tools/verify-all.sh
```

That runs all eleven static gates, including `verify-global-phone-directory.py`
(37 checks), `verify-connectivity.py` (49 checks),
`verify-email-auth.py` (76 checks), `verify-ui-redesign.py` (480 checks),
`verify-compile-surface.py` (387 checks) and 58 injected-defect negative tests.
These are static/source-level gates. A `dotnet build` and a real-device network
journey are still required before declaring production readiness; this workspace
does not have the Android/.NET 10 toolchain or a hosted backend.
