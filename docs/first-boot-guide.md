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

Cleartext HTTP to `10.0.2.2` / `localhost` is explicitly allowed by
`Platforms/Android/Resources/xml/network_security_config.xml` — for these two
dev addresses only. Nothing else runs without TLS.

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
| "No connection. Check your network…" on Send code | Backend not running → `docker compose up`; on a physical phone you forgot `adb reverse tcp:8080 tcp:8080` |
| Cleartext HTTP blocked | You're on a Release build (not supported yet) — build Debug |
| No OTP code arrives | By design: read it from the API logs (sandbox SMS) |
| App restores to sign-in after a phone restart | Keystore session lost/unavailable → sign in again (refresh token rotation is server-side and correct) |
| `docker compose up` port conflict 5432/8080 | Stop the local Postgres/other service on those ports or remap in compose |
| Emulator can't build: missing SDK | `dotnet build` first run downloads Android SDK bits — accept licenses when prompted |

## 7. Run the test suites (backend)

```bash
cd backend
dotnet test                     # unit tests: no Docker needed
docker info >/dev/null && dotnet test tests/KaamLink.IntegrationTests  # spins a real PostgreSQL container
```
