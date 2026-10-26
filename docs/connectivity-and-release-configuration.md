# Connectivity diagnosis and Release configuration

_Last reviewed: 2026-10-26 (branch `arena/b0f64897-kaamlink`)._

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
   look like a frozen app. Both HttpClients now share a 15-second timeout, the
   same `BaseAddress`, and a pre-flight network/configuration check.
4. **End-to-end phone login still needs a hosted backend.** The backend is
   present in this repository but is not deployed to a public HTTPS origin, and
   its development `Otp:Provider=log` writes codes to server logs instead of
   sending SMS. Those are infrastructure/provider prerequisites, not client
   bugs.

## What changed in the app

| Area | Before | Now |
|---|---|---|
| Release endpoint | Hardcoded reserved `.invalid` host | `-p:KaamLinkApiBaseUrl=https://api.example.com/` baked into `AssemblyMetadata`; unconfigured builds warn and show a localized setup error |
| Debug endpoint | Emulator/USB loopback only | Same loopback defaults, plus a Debug-only Preferences override or any LAN URL passed to the build property |
| Cleartext policy | One strict policy for all builds | Strict `network_security_config.xml` for Release; `network_security_config_debug.xml` + `AndroidManifest.Debug.xml` for Debug only |
| Transport | Typed client from `IHttpClientFactory`; static auth client from a second code path; default 100 s timeout | Both clients use `ApiHttp.Configure` (`BaseAddress` + 15 s timeout + JSON accept header); `SessionService` uses `IHttpClientFactory` |
| Failure states | "Offline" for DNS failure, timeout, airplane mode and missing config | Distinct `client.no_internet`, `client.api_not_configured` and offline codes with localized copy |
| Diagnostics | Base address only in Debug log | Base address, configuration source, network state and device type in Debug transport logs |
| Theme/shadows | Dispatcher condition inverted on background threads | Correct `IsDispatchRequired` branch |
| Lists | Default `CollectionView` measurement | `ItemSizingStrategy="MeasureFirstItem"` + `KeepScrollOffset` on the country picker and job lists |

## Build commands

### Release / staging (required for real phone login)

The URL must be **public HTTPS**, include the trailing slash, and point at the
KaamLink API root (the app appends `api/v1/...`). Do not use a LAN IP here.

```bash
dotnet build KaamLink.csproj -c Release -f net10.0-android \
  -p:KaamLinkStandaloneApk=true \
  -p:KaamLinkApiBaseUrl=https://api.example.com/
```

If the property is omitted, the build emits a warning and the APK installs but
the login screen reports **"Server not configured"** instead of pretending the
device is offline. That is deliberate: a silently unaddressable Release APK is
what caused the original confusion.

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

## Backend prerequisites (still required for production OTP)

Connectivity is only the first hop. For a real user to complete login:

1. Deploy `backend/` to a publicly reachable **HTTPS** origin.
2. Configure the API's database and JWT/refresh-token settings.
3. Replace development `Otp:Provider=log` with a production **SMS** provider and
   store its credentials as secrets/environment variables — never in the mobile
   app or this repository.
4. Keep verified phone/E.164 as the primary identity. Google must stay disabled
   until server-side ID-token validation and account linking exist (see
   `docs/auth-and-phone-login-decision.md`).
5. Verify `GET /health/ready`, then `POST api/v1/auth/otp/request` and
   `api/v1/auth/otp/verify` from a real phone on mobile data.

A Vercel/Supabase connection being available in the workspace is not a deployed
API. No public KaamLink backend URL or SMS credentials were supplied, so this
change cannot truthfully claim end-to-end production login is working. It does
remove the client-side causes, makes the remaining deployment step explicit,
and prevents another silently broken Release build.

## Verification run

```bash
bash tools/verify-all.sh
```

That runs all ten static gates, including `verify-connectivity.py` (41 checks) and
43 injected-defect negative tests. These are static/source-level gates. A `dotnet build` and a real-device network
journey are still required before declaring production readiness; this
workspace does not have the Android/.NET 10 toolchain or a hosted backend.
