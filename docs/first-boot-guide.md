# First Boot Guide — build, install, and walk through KaamLink end-to-end

Audience: developers (and the product owner). Time needed: ~30–45 minutes on a
clean machine. By the end you will have signed in with your own phone number,
completed onboarding as a worker AND registered a company as an employer, and
seen both homes render real server data.

> Known environment note: Phase 03 code was statically cross-verified
> (`python3 tools/verify-phase03.py` — 93 checks) but this repo environment has
> no .NET SDK. **The first `dotnet build` below is the formal verification gate.**
> If anything fails to compile, report the exact error — do not paper over it.

---

## 0. Prerequisites

| Tool | Why | Check |
|---|---|---|
| Docker Desktop (or Engine + compose plugin) | backend + database in one command | `docker compose version` |
| .NET 10 SDK + `maui-android` workload | build the app | `dotnet --version` (10.x), `dotnet workload list` |
| Android emulator (API 24+ recommended) **or** a physical Android 5.0+ phone with USB debugging | run the app | emulator: Android Studio → Device Manager; device: `adb devices` shows it |
| `adb` on PATH (comes with Android SDK platform-tools) | port forwarding for physical devices | `adb version` |

Install the workload once:

```bash
dotnet workload install maui-android
```

## 1. Start the backend (~2 minutes)

```bash
cd backend
cp .env.example .env          # dev placeholders — fine locally, never for production
docker compose up --build
```

Wait until you see `kaamlink-api | ... Now listening on: http://[::]:8080` and the
migration/seed log lines. Sanity checks in a second terminal:

```bash
curl -s http://localhost:8080/health/ready    # → Healthy
curl -s http://localhost:8080/api/v1/skills/catalog -o /dev/null -w '%{http_code}\n'   # → 401 (auth required — correct)
```

**Running the API from Visual Studio instead of Docker:** `backend/src/KaamLink.Api`
ships a `launchSettings.json` profile that pins `http://localhost:8080`, the same port
compose publishes, so F5 and the app agree — pick the **KaamLink.Api** profile and keep
the `Now listening on: http://localhost:8080` line in view. A different port (Kestrel's
default 5000–5300) is the most common reason the app reports "No connection" while the
API looks healthy in the browser. You still need PostgreSQL running (compose is the
simplest way to get it).

**Keep the compose terminal visible.** The OTP "SMS" is the logging sandbox in this
phase (`Otp:Provider=log`): the 6-digit code is printed in the API logs every time
you tap "Send code". A real SMS provider is a later-phase, paid integration (§35).

Optional API playground instead of the app: `backend/requests.http` walks every
endpoint with copy-paste requests (VS Code "REST Client" / Rider).

## 2. Deploy the app

**Android emulator (easiest):** the app already targets the emulator's host
loopback (`http://10.0.2.2:8080`). Just:

```bash
dotnet restore KaamLink.csproj
dotnet build KaamLink.csproj -f net10.0-android -c Debug -t:Run
```

(or open the repo in Visual Studio 2026 / VS Code + .NET MAUI extension, pick your
emulator, F5.)

**Physical Android phone:** the phone can't see `10.0.2.2`. Forward the port over USB
(the app's dev URL then resolves on the phone via `localhost`):

```bash
adb reverse tcp:8080 tcp:8080
dotnet build KaamLink.csproj -f net10.0-android -c Debug -t:Run
```

The app picks the URL itself, at runtime: **emulator → `http://10.0.2.2:8080/`,
physical device → `http://localhost:8080/`** (`Core/Api/ApiConfig.cs`). A LAN IP such as
`192.168.x.x` will **not** work: the cleartext policy below allows only those two hosts, so a
LAN URL fails every request. Use `adb reverse`, over USB or `adb connect`.

Cleartext HTTP to `10.0.2.2` / `localhost` is explicitly allowed by
`Platforms/Android/Resources/xml/network_security_config.xml` — for these two
dev addresses only. Nothing else runs without TLS.

**Installing an APK file directly (sideloading).** A normal Debug build uses *Fast
Deployment*: the managed assemblies stay **out of the `.apk`** and are pushed over adb by
`-t:Install`. Copying that `.apk` to a phone installs it and then it **closes immediately on
launch** — the runtime has no assemblies. That is a deployment choice, not a bug in the app
(dotnet/android#12011). For a self-contained `.apk`:

```bash
dotnet build KaamLink.csproj -c Debug -f net10.0-android -p:KaamLinkStandaloneApk=true
adb uninstall com.kaamlink.app          # ignore "not installed"
adb install -r bin/Debug/net10.0-android/com.kaamlink.app-Signed.apk
```

Play Protect will warn that a sideloaded Debug APK "may be harmful" — it is signed with your
local debug key and did not come from Play, which is expected. Choose **More details → Install
anyway** (or turn off Play Protect scanning while developing). Debug builds are not
distributable.

## 3. First boot walkthrough (worker)

1. **Role selection** — tap **"Find work"** (👷).
2. **Sign in** — enter any UAE-style mobile number, e.g. `0501234567`
   (the app normalizes it to `+971501234567`; the backend re-validates). Tap **Send code**.
3. **Read the OTP from the API logs** — in the compose terminal look for the warning line:

   ```
   kaamlink-api | warn: ...[OTP SANDBOX] Code for +971501234567 is 483920 (Otp:Provider=log — development only...)
   ```

   Enter it on the code screen. Wrong code → "The code doesn't match" (5 attempts max);
   the **Resend** button re-arms after the cooldown shown on it.
4. **Onboarding, step 1 (Basics)** — name, profession group + profession (from the
   seeded bilingual catalog), emirate, optional area → **Continue**. The server saves
   this step immediately (progressive onboarding — a crash loses nothing).
5. **Step 2 (Skills)** — your main profession is pre-added; set years, add more
   skills → **Continue**. Skills are stored as **Self-declared** — the passport never
   claims verification that didn't happen.
6. **Step 3 (Preferences)** — start availability (required), expected salary range
   and "about you" (optional) → **Finish setup**.
   Only when the **server** says onboarding is complete does the flow end.
7. **Worker home** — your name, profession · emirate, profile completeness bar at
   100%, the Skill Passport list (★ = main profession, "Self-declared" badges),
   availability + salary. Note the honest "job discovery arrives next phase" card.

## 4. Employer walkthrough

1. From worker home tap **"I'm also hiring workers"** (or sign in fresh and pick
   🏭 "Hire workers").
2. **Register your company** — name, business category, emirate, optional area and
   trade-licence number → **Submit for review**. The copy says it plainly: companies
   show as Verified **only after review**.
3. **Employer home** — your company card with an amber **Pending review** badge and
   your Owner role. Vacancy posting is next phase (also stated honestly).

**See verification happen (admin-lite):** register a second account with the dev
bootstrap admin phone `+971500000000` (listed in `appsettings.Development.json`),
then either replay `backend/requests.http` → *ADMIN: approve a company*, or:

```bash
# 1) get an admin token (code from API logs), then:
curl -s http://localhost:8080/api/v1/admin/companies/pending -H "Authorization: Bearer $ADMIN"
curl -s -X POST http://localhost:8080/api/v1/admin/companies/<COMPANY_ID>/review \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"approve": true}'
```

Reopen employer home (it refreshes on every appearance): the badge turns green
**Verified**. Rejection requires a reason (`{"approve": false, "reason": "…"}`) and
the reason is then visible to the employer.

## 5. What is deliberately NOT in this build (no fake functionality, §50)

- Jobs discovery, applications, vacancy posting, assessments, documents, payments —
  later phases per the roadmap. Homes say so explicitly instead of pretending.
- Real SMS delivery — OTP codes are in the API logs (sandbox provider).
- Production hosting — Release builds have no API target yet (Phase 06 environments).
- iOS — Android-only V1 by product decision (ADR 0001).

## 6. Troubleshooting

| Symptom | Likely cause → fix |
|---|---|
| "No connection" on Send code | The app reached *no* server, so this is always the transport, never the phone number. In order: (1) is the API up? `curl -s http://localhost:8080/health/ready` → `Healthy`; (2) `docker compose ps` shows `kaamlink-api` **running** (not restarting); (3) if you started the API from Visual Studio, use the pinned **KaamLink.Api** profile — it binds 8080, a Kestrel default (5000–5300) will not work; (4) physical phone → `adb reverse tcp:8080 tcp:8080` |
| Which URL did the app actually try? | Debug builds log every transport failure: `adb logcat -s DOTNET | grep KaamLink` (PowerShell: `adb logcat -s DOTNET | Select-String KaamLink`) → `[KaamLink] POST http://10.0.2.2:8080/api/v1/auth/otp/request failed: …`. Emulator images see the host as **10.0.2.2**, a physical phone via `adb reverse` as **localhost** — neither can use the other's address |
| Cleartext HTTP blocked | You're on a Release build (not supported yet) — build Debug |
| No OTP code arrives | By design: read it from the API logs (sandbox SMS) |
| **`NU1101/NU1102` "Unable to find package …" on many/all packages** | Wrong/incomplete NuGet sources or SDK — diagnose below (§6a) |
| `NETSDK1045` / "The current .NET SDK does not support targeting .NET 10.0" | SDK too old — install the **.NET 10 SDK** (the repo's `global.json` will now say exactly this) |
| `Microsoft.Maui.Controls` not found / `maui-android` workload errors | Workload missing — `dotnet workload install maui-android` (repair: `dotnet workload repair`) |
| App restores to sign-in after a phone restart | Keystore session lost/unavailable → sign in again (refresh token rotation is server-side and correct) |
| `docker compose up` port conflict 5432/8080 | Stop the local Postgres/other service on those ports or remap in compose |
| **"App not installed"** when installing on the phone | Usually a stale or partial APK, or a signature clash with an older install. Uninstall first (`adb uninstall com.kaamlink.app`), confirm the build actually succeeded, then install the `-Signed.apk` |
| **Play Protect: "app may be harmful"** | Expected for a sideloaded Debug APK (local debug key, not from Play). **More details → Install anyway** |
| **App installs, then closes immediately** | Fast Deployment built an APK with no assemblies inside it. Rebuild with `-p:KaamLinkStandaloneApk=true`, or deploy with `-t:Install` (see §2) |
| **"No connection" on a phone while the emulator works** | The phone resolves `localhost`; run `adb reverse tcp:8080 tcp:8080`. A LAN IP won't work — the cleartext policy allows only `10.0.2.2`/`localhost` |
| **Need the crash stack** | `adb logcat | grep KaamLink` (PowerShell: `Select-String KaamLink`). The app now logs `[KaamLink] UNHANDLED EXCEPTION: …` before it dies |
| **`XAGR7019` ... `UnauthorizedAccessException` ... `obj\…\lp\…`** | A file in `obj` is locked: close Visual Studio, end any `MSBuild.exe`/`dotnet.exe`, delete `obj` and `bin`, rebuild. Add the repo folder to Defender's exclusions if it keeps happening |
| **`XAGR7028`: "Could not find file …\obj\Debug\net10.0-android\lp\<n>\jl\res\…xml"** | **Stale build intermediates, not a code error.** `lp\…` is the extracted resource tree of the NuGet/AndroidX libraries; it is written by the build and can be left half-populated when a build is interrupted (for example by C# errors) or when two builds run at once. Close Visual Studio, delete `obj` and `bin`, rebuild. `dotnet clean` alone often leaves `lp` behind. If it returns, check that OneDrive/antivirus is not syncing `obj`, and that the path is under the Windows long-path limit |
| Emulator can't build: missing SDK | `dotnet build` first run downloads Android SDK bits — accept licenses when prompted |

### 6a. "Package/repository could not be found" diagnostics (in order)

1. **SDK check** — `dotnet --list-sdks` must show a `10.0.x` entry. No 10.x → install
   the .NET 10 SDK, then re-run. (Without it you get dozens of misleading errors.)
2. **Feed check** — `dotnet nuget list source` must include
   `https://api.nuget.org/v3/index.json`. The repo ships its own `NuGet.config` so
   this should be automatic; if your company PC forces an internal mirror, restore
   must go through that mirror instead (admin help needed).
3. **Workload check** — `dotnet workload list` must list `maui-android`.
   If it errors or it's missing: `dotnet workload install maui-android`.
4. **Clean retry** — `dotnet restore KaamLink.csproj --no-cache`.
5. If it still fails, copy the **first 5 error lines verbatim** and report them —
   guesses at this point waste your time and mine.

## 7. Run the test suites (backend)

```bash
cd backend
dotnet test                     # unit tests: no Docker needed
docker info >/dev/null && dotnet test tests/KaamLink.IntegrationTests  # spins a real PostgreSQL container
```
