# Phase 06 report — Aurora Clay, email magic link and terms gate

**Date:** 2026-10-27  
**Branch:** `arena/b0f64897-kaamlink`

## User-visible outcome

- The current login preview is shown in
  [`login-and-theme-preview.md`](login-and-theme-preview.md). It is rendered from
  the shipped strings and palettes, not from the stale `mockups/02-login.png`.
- **Continue with Google** remains disabled and now explains why: no Google Cloud
  OAuth client and no server-side ID-token verification/account linking. Enabling
  it without those would be fake authentication.
- A **free/easy signup and login method is implemented**: Supabase email magic
  link. It is passwordless, uses a public publishable/anon key, and deep-links back
  to `kaamlink://auth-callback`.
- A **tickable Terms and Conditions checkbox** gates both email and phone sign-in.
  Acceptance is persisted so the email-app round trip still completes the login.
- The neumorphism/clay theme was upgraded from blue-grey to **Aurora Clay**:
  indigo/violet gradients, cyan/mint accents, lavender light surfaces, rich
  midnight-violet dusk (not black), 24dp buttons, 28dp cards/banners and a
  refreshed clay shadow ramp.
- Day/Dusk still uses compiled palette dictionaries and native buttons; the
  previous crash-hardening fixes remain in place.

## Architecture

| Concern | Implementation |
|---|---|
| Magic-link request | `Core/Api/SupabaseMagicLinkService` → `POST /auth/v1/otp` |
| Deep link | `Platforms/Android/MainActivity` intent filter + `App.OnAppLinkRequestReceived` |
| Android intent forwarding | `MauiProgram.ConfigureLifecycleEvents` forwards `OnCreate` / `OnNewIntent` VIEW intents to `App.SendOnAppLinkRequestReceived` |
| Callback parsing | `Core/Api/EmailAuthLinkService` (fragment + query, URL-decoded, one-shot pending store) |
| Backend verification | `KaamLink.Infrastructure.Identity.SupabaseUserVerifier` → GoTrue `/auth/v1/user` |
| Session exchange | `POST api/v1/auth/email/exchange` → `AuthService.ExchangeSupabaseTokenAsync` |
| Identity mapping | New nullable `users.email` + unique `ux_users_email`; email-only accounts get a synthetic `+999...` phone |
| Client session | KaamLink's own access/refresh token pair via `SessionService.CompleteLoginAsync` |
| Terms gate | `LoginViewModel.TermsAccepted` + `CheckBox` + `Preferences` persistence |

Supabase access tokens are never accepted directly by KaamLink API endpoints. The
backend verifies them with GoTrue before issuing its own session tokens.

Supabase connector configuration performed: project `kaamlink-magic-link`
(`sdxywcosqidyopbfevoe`) created and healthy, email auth/signup enabled, email
confirmation required, and `kaamlink://auth-callback` set as site URL / redirect
allowlist. No service-role key is committed.

## Static verification

`bash tools/verify-all.sh` — **ALL STATIC GATES PASSED**:

| Gate | Result |
|---|---|
| `verify-phase03.py` | 105 checks, 0 failures |
| `verify-phase04.py` | 105 checks, 0 failures |
| `verify-phase05.py` | 88 checks, 0 failures |
| `verify-bindings.py` | 15 pages, 347 types, 377 compiled bindings |
| `verify-theme-contrast.py` | 55 checks, 0 failures across 2 palettes |
| `verify-ui-redesign.py` | 480 checks, 0 failures |
| `verify-global-phone-directory.py` | 37 checks, 0 failures |
| `verify-compile-surface.py` | 387 checks, 0 failures |
| `verify-connectivity.py` | 49 checks, 0 failures |
| `verify-email-auth.py` | 76 checks, 0 failures |
| `verify-gates-selftest.py` | 58 injected defects detected; tree restored clean |

New negative tests cover redirect-URI drift, a terms gate that stops gating, a
backend exchange that skips verification, a service-role key in the client, and a
missing deep-link handler.

## Remaining verification (explicitly not done here)

- `dotnet build` / XamlC compilation and Android APK install.
- Backend startup and Phase 06 migration against a real PostgreSQL instance.
- Live Supabase email delivery and a device deep-link round trip.
- Production custom SMTP for magic links and a production SMS provider for phone OTP.
- Public HTTPS KaamLink API deployment (`-p:KaamLinkApiBaseUrl=https://.../`).
- Arabic/RTL, large-font and screen-reader review on a device.

Static gates are not a substitute for those runtime checks. The setup checklist is
in [`docs/email-magic-link-setup.md`](../email-magic-link-setup.md) and
[`docs/connectivity-and-release-configuration.md`](../connectivity-and-release-configuration.md).
