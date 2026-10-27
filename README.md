# KaamLink

**Direct hiring for practical work in the UAE.**

KaamLink is an Android-first mobile platform connecting workers (factory, workshop,
construction, warehouse, installation, technician, helper and similar occupations)
with employers (factories, workshops, contractors, SMEs, warehouses) — built around
**visible, fairly-assessed practical skill** instead of unnecessary education filters.

> Show your skills. Find genuine work. Get assessed fairly.
> Find suitable workers. Assess practical ability. Hire confidently.

## Status

**Phase 06 — Supabase email magic-link signup/login, tickable terms gate and Aurora Clay theme live.**

* **Phase 01** — foundation: sample code removed, clean Android-first MAUI shell
  → [docs/phase-01-report.md](docs/phase-01-report.md)
* **Phase 02** — backend: phone-OTP auth (JWT + rotating refresh tokens), audit log,
  rate limiting, CI, docker-compose → [docs/phase-02-report.md](docs/phase-02-report.md)
* **Phase 03** — onboarding on both sides: OTP sign-in in the app; worker
  3-step onboarding (Skill Passport); employer company registration; admin
  verification-lite; seeded bilingual occupation catalog
  → [docs/phase-03-report.md](docs/phase-03-report.md)
* **Phase 04** — jobs core: employer vacancy posting (verification-gated publishing,
  role matrix), worker feed with filters, apply, employer applicant decisions,
  withdrawal → [docs/phase-04-report.md](docs/phase-04-report.md)
* **Phase 05** — skill assessments: seeded bilingual question banks, single-shot
  server-scored attempts (no correct answers ever leave the server), pass flips a
  self-declared skill to Assessment Verified
  → [docs/phase-05-report.md](docs/phase-05-report.md)
* **Phase 06** — free passwordless Supabase email magic-link auth, Android deep-link
  callback, backend token verification/exchange, `users.email` migration, tickable
  terms gate, and the richer Aurora Clay / Aurora Dusk palettes
  → [docs/email-magic-link-setup.md](docs/email-magic-link-setup.md)

**UI redesign — every active screen rebuilt on one design system (Aurora Clay, Lucide icons,
EN/AR, 48dp targets, truthful states):** audit, 23 mockups, design-system spec and the
implementation/verification reports live in [docs/ui-redesign/](docs/ui-redesign);
the latest Phase 06 report is [06-aurora-email-auth-report.md](docs/ui-redesign/06-aurora-email-auth-report.md).
Static gates: `bash tools/verify-all.sh` runs `verify-phase03.py` (105),
`verify-phase04.py` (105), `verify-phase05.py` (88), `verify-bindings.py` (377 compiled
bindings), `verify-theme-contrast.py` (55), `verify-ui-redesign.py` (480),
`verify-global-phone-directory.py` (37), `verify-compile-surface.py` (387),
`verify-connectivity.py` (49) and `verify-email-auth.py` (76), plus 58 negative gate
self-tests. Connectivity/Release and Supabase setup are documented in
[docs/connectivity-and-release-configuration.md](docs/connectivity-and-release-configuration.md)
and [docs/email-magic-link-setup.md](docs/email-magic-link-setup.md).
**The redesign has not been built or run in this environment: no .NET/Android SDK** —
see §4 of the report.

**Want to run it? → [docs/first-boot-guide.md](docs/first-boot-guide.md)**
(backend up → app on emulator/device → sign in with your phone → onboard → home).

What exists today:

* Android-first .NET MAUI app: role selection → Supabase email magic-link or
  phone-OTP sign-in (both require a ticked terms checkbox) → guided onboarding →
  homes, session kept in the Android Keystore (secure storage), silent token refresh
* Backend API (ASP.NET Core 10 + PostgreSQL): phone OTP and Supabase token
  exchange/verification, worker profiles, companies, admin review queue, bilingual
  skill catalog (seeded), vacancies, applications, skill assessments
* Jobs core live: vacancy posting → worker feed with filters → apply → employer
  applicant decisions → withdrawal
* Aurora Clay design system (indigo/violet + cyan/mint, rich dark violet dusk,
  rounder shapes), full EN/AR localization, centralized routes/strings/status labels
* Honest placeholders only — anything not yet built says so (§50)

## Stack (V1)

| Layer     | Choice                                        | Notes |
|-----------|-----------------------------------------------|-------|
| Client    | .NET MAUI (net10.0-android), XAML + MVVM      | Android-only in V1; min SDK 21 |
| MVVM      | CommunityToolkit.Mvvm (source generators)     | `[ObservableProperty]` / `[RelayCommand]` |
| Strings   | .resx (en neutral + ar satellite)             | RTL supported (`supportsRtl=true`) |
| Backend   | ASP.NET Core 10 + PostgreSQL 16 (modular monolith) | see `backend/README.md` |

## Repository layout

```
KaamLink.csproj          Android-only MAUI project (RootNamespace: KaamLink)
App.xaml(.cs)            App entry; converters + merges Colors.xaml + Styles.xaml
AppShell.xaml(.cs)       Shell root — all pages are top-level routes (absolute navigation)
MauiProgram.cs           App composition root (RegisterServices)
Core/
  Navigation/            AppRoutes (route registry), RoleNavigator (role gating)
  Api/                   ApiClient + DTOs (source-gen JSON), SessionService, token store,
                         bearer/refresh handler, ApiException, SupabaseConfig,
                         SupabaseMagicLinkService, EmailAuthLinkService — URLs live here
  Presentation/          EnumDisplay (enum ↔ localized labels), converters, localization helpers
  Text/                  PhoneInput (mirrors backend phone normalization)
Features/
  Onboarding/            Role selection (worker vs employer entry)
  Auth/                  Email magic-link / phone-OTP login, terms checkbox, OTP verify (resend cooldown)
  Worker/                WorkerOnboarding (3 steps) + WorkerHome (live profile)
  Employer/              EmployerOnboarding (company) + EmployerHome (verification state)
Resources/
  Styles/                Colors.xaml (Aurora Clay tokens), Styles.xaml (rounded design system)
  Themes/                ClayDusk.xaml (Aurora Dusk rich dark palette)
  Strings/               AppStrings.resx (en) · AppStrings.ar.resx · committed Designer.cs
  Fonts/                 OpenSans Regular/Semibold
  AppIcon/ Splash/       Aurora Clay linked-K brand mark (SVG)
Platforms/Android/       Manifest, network_security_config (cleartext = dev machine only)
backend/                 ASP.NET Core API, EF Core migrations, tests, docker-compose
docs/                    Phase reports, ADRs, first-boot guide
tools/                   Static cross-verification (runs without a .NET SDK)
ci/                      CI workflow definitions (activate per ci/README.md)
```

## Build

Prerequisites: .NET 10 SDK + `maui-android` workload (includes OpenJDK + Android SDK
provisioning).

```bash
dotnet workload install maui-android
dotnet restore KaamLink.csproj
dotnet build KaamLink.csproj -f net10.0-android -c Debug
```

Run on a device/emulator (one-time per device): `dotnet build -t:Run -f net10.0-android`
(or use your IDE's deploy). For a Release APK that can reach a real backend, pass
`-p:KaamLinkApiBaseUrl=https://your-api.example.com/`. Public Supabase URL/anon-key
defaults are baked in; override them with `-p:KaamLinkSupabaseUrl=...` and
`-p:KaamLinkSupabaseAnonKey=...` when changing projects.

The app needs the backend: `cd backend && cp .env.example .env && docker compose up --build`
(API on http://localhost:8080; OTP codes appear in the API logs while the SMS sandbox is `log`).
Email magic links are sent by the configured Supabase project; apply the Phase 06
migration and set `Supabase__Url` / `Supabase__AnonKey` in the deployed API.
**Complete walkthroughs: [docs/first-boot-guide.md](docs/first-boot-guide.md),
[docs/email-magic-link-setup.md](docs/email-magic-link-setup.md) and
[docs/connectivity-and-release-configuration.md](docs/connectivity-and-release-configuration.md).**

> Note: this repository environment has no .NET/Android SDK, so code is authored
> compile-correct and statically cross-verified (`bash tools/verify-all.sh`, all
> eleven gates green). The first `dotnet build` locally/CI and a device deep-link
> round trip remain the formal verification gates.

## Working agreements

* **No fake functionality** — UI never claims an action the server hasn't confirmed.
* **No magic strings** — routes via `AppRoutes`, user-facing copy via `AppStrings`,
  colors/styles via `Resources/Styles`.
* **Secrets stay out of git** — keystores, `*.env`, `appsettings.*.local.json` are ignored.
* Backend business rules live on the backend; Android enforces UX, never security.
