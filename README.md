# KaamLink

**Direct hiring for practical work in the UAE.**

KaamLink is an Android-first mobile platform connecting workers (factory, workshop,
construction, warehouse, installation, technician, helper and similar occupations)
with employers (factories, workshops, contractors, SMEs, warehouses) — built around
**visible, fairly-assessed practical skill** instead of unnecessary education filters.

> Show your skills. Find genuine work. Get assessed fairly.
> Find suitable workers. Assess practical ability. Hire confidently.

## Status

**Phase 01 — foundation complete.** The repository previously contained Microsoft
sample/template code (a .NET MAUI workshop task-tracker unrelated to this product);
it has been fully removed and replaced with a clean, minimal, compiling foundation.

What exists today (intentionally small):

* Android-first .NET MAUI app shell with dependency injection
* Role-separated entry point: **Looking for work** / **Hiring workers**
  (Worker and Employer are separate experiences with separate navigation roots)
* Navigation-skeleton pages for the Worker and Employer experience roots
  (honest placeholders — no fake functionality; features land in later phases)
* Design-system tokens (colors, typography, card/button styles) in `Resources/Styles`
* Localization infrastructure — English + Arabic (RTL), `Resources/Strings`
* Centralized route registry (`Core/Navigation/AppRoutes.cs`)

Full audit, architecture and roadmap: **[docs/phase-01-report.md](docs/phase-01-report.md)**
Architecture decisions: **[docs/adr](docs/adr)**

## Stack (V1)

| Layer     | Choice                                        | Notes |
|-----------|-----------------------------------------------|-------|
| Client    | .NET MAUI (net10.0-android), XAML + MVVM      | Android-only in V1; min SDK 21 |
| MVVM      | CommunityToolkit.Mvvm (source generators)     | `[ObservableProperty]` / `[RelayCommand]` |
| Strings   | .resx (en neutral + ar satellite)             | RTL supported (`supportsRtl=true`) |
| Backend   | *Not built yet* — ASP.NET Core + PostgreSQL (recommended) | See phase-01 report §E/§J |

## Repository layout

```
KaamLink.csproj          Android-only MAUI project (RootNamespace: KaamLink)
App.xaml(.cs)            App entry; merges Colors.xaml + Styles.xaml
AppShell.xaml(.cs)       Shell root — role-selection page + route registration
MauiProgram.cs           App composition root (DI registrations: RegisterServices)
Core/                    Cross-feature app concerns
  Navigation/AppRoutes.cs  Central route-name registry (no magic strings)
Features/
  Onboarding/            Role selection (worker vs employer entry)
  Worker/                Worker experience root (WorkerHome*)
  Employer/              Employer experience root (EmployerHome*)
Resources/
  Styles/                Colors.xaml (design tokens), Styles.xaml (design system)
  Strings/               AppStrings.resx (en) · AppStrings.ar.resx · committed Designer.cs
  Fonts/                 OpenSans Regular/Semibold
  AppIcon/ Splash/       Interim brand mark (replace in branding phase)
Platforms/Android/       Manifest (min permissions, allowBackup=false), colors
docs/                    Phase 01 report + ADRs
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

> Note: this environment has no .NET SDK and no package/workload egress, so the
> foundation was authored compile-correct but could not be built here. Run the
> commands above on a developer machine/CI as the first verification step.

## Working agreements

* **No fake functionality** — UI never claims an action the server hasn't confirmed.
* **No magic strings** — routes via `AppRoutes`, user-facing copy via `AppStrings`,
  colors/styles via `Resources/Styles`.
* **Secrets stay out of git** — keystores, `*.env`, `appsettings.*.local.json` are ignored.
* Backend business rules live on the backend; Android enforces UX, never security.
