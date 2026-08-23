# ADR 0001: Keep .NET MAUI as the V1 client (Android-only target)

## Status
Accepted (Phase 01)

## Context
The repo was bootstrapped on .NET MAUI (net10.0-android) but contained only Microsoft
sample code — so there was effectively a free choice of client stack. The product is
Android-first, targeting affordable devices on slow networks, with future web clients
(web employer/admin portals) planned. Options: native Kotlin/Compose, Flutter, .NET MAUI.

## Decision
Keep **.NET MAUI**, targeting **only `net10.0-android`** in V1.

## Rationale
- Meets every stated V1 requirement: Android-first UX, offline (SQLite later), Arabic/RTL,
  push notifications, secure storage, low-end devices (min SDK 21), AOT/profiled-AOT for
  startup and memory.
- The organization already bootstrapped this repo on MAUI — switching stacks without a
  measured, product-grounded reason would be churn, not engineering.
- C# end-to-end with the recommended ASP.NET Core backend enables shared DTO contracts
  and one hiring pipeline for talent.
- There was no product code to lose; the sample/template code was deleted (it carried no
  domain value and did not compile as committed).

## Risks / mitigations
- **APK size & cold start on low-end devices** → profiled AOT, trimming, Resource
  shrinking, measured on a low-end reference device before launch gate; revisit if the
  startup/memory budget (see report §O, R-10) cannot be met.
- **Smaller MAUI hiring pool than Kotlin/Flutter** → documented conventions + MVVM
  toolkit reduce ramp-up; one language for client+backend partially offsets it.
- **Framework maturity issues** → pin to serviced .NET 10 SDK bands; verify per-upgrade
  in CI on a device farm.

## Alternatives considered
- Kotlin/Compose native: best raw low-end performance, but doubles the language count
  (backend still needed) and loses shared contracts.
- Flutter: strong UI throughput, Dart hiring pool similar to MAUI in the region; no
  decisive advantage for this product's mostly form/list workflows.
