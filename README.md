# KaamLink

**Direct hiring for practical work in the UAE.**

KaamLink is an Android-first mobile platform connecting workers (factory, workshop,
construction, warehouse, installation, technician, helper and similar occupations)
with employers (factories, workshops, contractors, SMEs, warehouses) — built around
**visible, fairly-assessed practical skill** instead of unnecessary education filters.

> Show your skills. Find genuine work. Get assessed fairly.
> Find suitable workers. Assess practical ability. Hire confidently.

## Status

**Phase 03 — onboarding complete; the app is ready for its first boot.**

* **Phase 01** — foundation: sample code removed, clean Android-first MAUI shell
  → [docs/phase-01-report.md](docs/phase-01-report.md)
* **Phase 02** — backend: phone-OTP auth (JWT + rotating refresh tokens), audit log,
  rate limiting, CI, docker-compose → [docs/phase-02-report.md](docs/phase-02-report.md)
* **Phase 03** — onboarding on both sides: OTP sign-in in the app; worker
  3-step onboarding (Skill Passport); employer company registration; admin
  verification-lite; seeded bilingual occupation catalog
  → [docs/phase-03-report.md](docs/phase-03-report.md)

**Want to run it? → [docs/first-boot-guide.md](docs/first-boot-guide.md)**
(backend up → app on emulator/device → sign in with your phone → onboard → home).

What exists today:

* Android-first .NET MAUI app: role selection → phone-OTP sign-in → guided onboarding
  → homes, session kept in the Android Keystore (secure storage), silent token refresh
* Backend API (ASP.NET Core 10 + PostgreSQL): auth, worker profiles, companies,
  admin review queue, bilingual skill catalog (seeded)
* Design system + full EN/AR localization, centralized routes/strings/status labels
* Honest placeholders only — job discovery and vacancy posting arrive next phase (§50)

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
                         bearer/refresh handler, ApiException — the only place URLs live
  Presentation/          EnumDisplay (enum ↔ localized labels), converters, localization helpers
  Text/                  PhoneInput (mirrors backend phone normalization)
Features/
  Onboarding/            Role selection (worker vs employer entry)
  Auth/                  Phone login + OTP verify (resend cooldown)
  Worker/                WorkerOnboarding (3 steps) + WorkerHome (live profile)
  Employer/              EmployerOnboarding (company) + EmployerHome (verification state)
Resources/
  Styles/                Colors.xaml (design tokens), Styles.xaml (design system)
  Strings/               AppStrings.resx (en) · AppStrings.ar.resx · committed Designer.cs
  Fonts/                 OpenSans Regular/Semibold
  AppIcon/ Splash/       Interim brand mark (replace in branding phase)
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
(or use your IDE's deploy). Package for release: `dotnet publish -f net10.0-android -c Release`.

The app needs the backend: `cd backend && cp .env.example .env && docker compose up --build`
(API on http://localhost:8080; OTP codes appear in the API logs while the SMS sandbox is `log`).
**Complete walkthrough: [docs/first-boot-guide.md](docs/first-boot-guide.md).**

> Note: this repository environment has no .NET SDK and no package/workload egress, so code
> is authored compile-correct and statically cross-verified (`tools/verify-phase03.py`,
> 93 checks) but the first `dotnet build` locally/CI is the formal verification gate.

## Working agreements

* **No fake functionality** — UI never claims an action the server hasn't confirmed.
* **No magic strings** — routes via `AppRoutes`, user-facing copy via `AppStrings`,
  colors/styles via `Resources/Styles`.
* **Secrets stay out of git** — keystores, `*.env`, `appsettings.*.local.json` are ignored.
* Backend business rules live on the backend; Android enforces UX, never security.
