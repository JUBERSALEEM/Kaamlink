# Phase 06 report — Aurora Clay, email OTP and password sign-in

**Date:** 2026-10-27  
**Branch:** `arena/b0f64897-kaamlink`

## User-visible outcome

- The current login preview is shown in
  [`login-and-theme-preview.md`](login-and-theme-preview.md). It is rendered from
  the shipped strings and palettes, not from the stale `mockups/02-login.png`.
- **Continue with Google** remains disabled and explains why: no Google Cloud
  OAuth client and no server-side ID-token verification/account linking.
- The empty “confirm your email address” click page is no longer part of the
  active flow. Supabase now sends an **email one-time code (OTP)** instead of a
  click-to-sign-in link.
- The user ticks the terms checkbox, requests a code, enters the 6–8 digit code
  inline, and is signed in.
- After first confirmation, the app offers **password creation** so later
  sessions can use email + password instead of requesting another OTP.
- The neumorphism/clay theme remains **Aurora Clay**: indigo/violet gradients,
  cyan/mint accents, lavender light surfaces, rich midnight-violet dusk (not
  black), 24dp buttons, 28dp cards/banners and a refreshed clay shadow ramp.

## Architecture

| Concern | Implementation |
|---|---|
| Send OTP | `Core/Api/SupabaseEmailAuthService` → `POST /auth/v1/otp` |
| Verify OTP | `POST /auth/v1/verify` with `{ email, token, type: "email" }` |
| Password creation | `PUT /auth/v1/user` with `password` + `data.password_set = true` |
| Password login | `POST /auth/v1/token?grant_type=password` |
| Backend verification | `KaamLink.Infrastructure.Identity.SupabaseUserVerifier` → GoTrue `/auth/v1/user` |
| Session exchange | `POST api/v1/auth/email/exchange` → `AuthService.ExchangeSupabaseTokenAsync` |
| Password-setup flag | `user_metadata.password_set` → `AuthTokensResponse.RequiresPasswordSetup` |
| Identity mapping | Nullable `users.email` + unique `ux_users_email`; synthetic `+999...` phone for email-only users |
| Client session | KaamLink's own access/refresh token pair via `SessionService.CompleteLoginAsync` |
| Legacy deep link | `Platforms/Android/MainActivity` + `App.OnAppLinkRequestReceived` (fallback only) |
| Android intent forwarding | `MauiProgram.ConfigureLifecycleEvents` forwards `OnCreate` / `OnNewIntent` VIEW intents |
| Terms gate | `LoginViewModel.TermsAccepted` + `CheckBox` + `Preferences` persistence |

Supabase access tokens are never accepted directly by KaamLink API endpoints. The
backend verifies them with GoTrue before issuing its own session tokens.

## Supabase changes performed

A fresh dedicated project `kaamlink-mobile-auth` (`zyklrndmmcmsktagkzek`) was
created; the old `kaamlink-magic-link` project was deleted and the unrelated
`Proglass Automation` project is not used. A dedicated
publishable key `kaamlink_mobile`
(`sb_publishable_FV1pX1Xo3QCxCabFetDhIg_RszBDMab`) is the only Supabase key in
the client.

Connector-supported config applied to the new project:

- `external_email_enabled: true`
- `disable_signup: false`
- `mailer_autoconfirm: false`
- `external_phone_enabled: false`, `sms_autoconfirm: false`
- `jwt_exp: 3600`
- effective `refresh_token_rotation_enabled: true`,
  `security_refresh_token_reuse_interval: 10`
- `site_url`: `https://zyklrndmmcmsktagkzek.supabase.co/functions/v1/auth-confirmed`
- `uri_allow_list`: `kaamlink://auth-callback`, `kaamlink://auth-confirmed` +
  the `auth-confirmed` URL

Edge Functions deployed:

- `auth-confirmed` — a real HTML page so any leftover confirmation link is not blank.
- `configure-otp-templates` v2 — protected helper that applies the OTP templates
  from inside Supabase. It requires the `x-admin-token` header matching the
  `OTP_TEMPLATE_ADMIN_TOKEN` secret and reads the scoped Management API PAT from
  the `SUPABASE_ACCESS_TOKEN` secret.

Hosted email templates and `mailer_otp_length` cannot be changed by the connected
Supabase tool. The repo includes `tools/configure-supabase-otp-templates.py`
which PATCHes the Management API with a scoped PAT. Run it once:

```bash
export SUPABASE_ACCESS_TOKEN=sbp_your_scoped_token_here
python3 tools/configure-supabase-otp-templates.py
```

Do not paste the token into chat or commit it. The script changes **both** the
Magic Link/OTP and Confirm signup templates to `{{ .Token }}`, keeps the hosted
8-digit OTP length, and raises the password minimum to 8. Until it runs, the
default `password_min_length` is 6 and the default emails still contain a click
link; the app accepts 8-digit codes. Full steps:
[`docs/email-magic-link-setup.md`](../email-magic-link-setup.md).

## Static verification

`bash tools/verify-all.sh` — **ALL STATIC GATES PASSED**:

| Gate | Result |
|---|---|
| `verify-phase03.py` | 105 checks, 0 failures |
| `verify-phase04.py` | 105 checks, 0 failures |
| `verify-phase05.py` | 88 checks, 0 failures |
| `verify-bindings.py` | 15 pages, 353 types, 403 compiled bindings |
| `verify-theme-contrast.py` | 55 checks, 0 failures across 2 palettes |
| `verify-ui-redesign.py` | 499 checks, 0 failures |
| `verify-global-phone-directory.py` | 37 checks, 0 failures |
| `verify-compile-surface.py` | 416 checks, 0 failures |
| `verify-connectivity.py` | 49 checks, 0 failures |
| `verify-email-auth.py` | 135 checks, 0 failures |
| `verify-gates-selftest.py` | 64 injected defects detected; tree restored clean |

New negative tests cover OTP command binding, password-setup backend flag,
confirm-signup template, Management API token, Android intent forwarding,
helper Edge Function admin-token protection and fresh-project targeting.

## Remaining verification (explicitly not done here)

- `dotnet build` / XamlC compilation and Android APK install.
- Backend startup and Phase 06 migration against a real PostgreSQL instance.
- Hosted Supabase email-template PATCH (needs the PAT step above).
- Live Supabase email delivery and a device OTP round trip.
- Device password creation + password login round trip.
- Production custom SMTP for OTP emails and a production SMS provider for phone OTP.
- Public HTTPS KaamLink API deployment (`-p:KaamLinkApiBaseUrl=https://.../`).
- Arabic/RTL, large-font and screen-reader review on a device.

Static gates are not a substitute for those runtime checks. Setup details are in
[`docs/email-magic-link-setup.md`](../email-magic-link-setup.md) and
[`docs/connectivity-and-release-configuration.md`](../connectivity-and-release-configuration.md).
