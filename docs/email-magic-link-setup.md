# KaamLink email OTP + password sign-in setup

> File name is kept as `email-magic-link-setup.md` for existing links. The active
> flow is **email one-time code (OTP)**, not a click-to-sign-in link.

## What changed

The old Supabase magic-link email contained a `{{ .ConfirmationURL }}` link.
Clicking it on a desktop/browser could land on an empty or unusable page. The app
now asks Supabase for an **email OTP**, the user types the 8-digit code in the
app, and after first confirmation the user can create a password for later
password sign-in.

Flow:

1. User ticks the Terms and Conditions checkbox.
2. User enters an email and taps **Email me a code**.
3. App calls `POST /auth/v1/otp` with `{ email, create_user: true }`.
4. Supabase emails a one-time code using a template that renders `{{ .Token }}`.
5. User enters the code in the app; app calls `POST /auth/v1/verify` with
   `{ email, token, type: "email" }`.
6. App sends the returned Supabase access token to
   `POST /api/v1/auth/email/exchange`.
7. Backend verifies the token with GoTrue `/auth/v1/user`, then maps/creates the
   KaamLink user and issues KaamLink's own JWT + rotating refresh token.
8. If `user_metadata.password_set` is missing/false, the backend returns
   `requiresPasswordSetup: true`. The app shows **Set a password for next time**.
9. `PUT /auth/v1/user` sets the password and `data: { password_set: true }`.
10. Later sign-in uses `POST /auth/v1/token?grant_type=password` with email +
    password, then the same KaamLink exchange endpoint.

Supabase access tokens are **not** accepted directly by other KaamLink API
endpoints. The exchange is mandatory.

## Dedicated Supabase project

KaamLink now uses a fresh, dedicated Free project. The unrelated
`Proglass Automation` / `afpbdyamedpanwijxghk` project is **not** used for
KaamLink. The earlier `kaamlink-magic-link` project was deleted to free the
Free-tier project slot.

- Project: `kaamlink-mobile-auth`
- Ref: `zyklrndmmcmsktagkzek`
- Region: `ap-south-1`
- Public URL: `https://zyklrndmmcmsktagkzek.supabase.co`
- Dedicated publishable key name: `kaamlink_mobile`
- Publishable key (public, safe to ship): `sb_publishable_FV1pX1Xo3QCxCabFetDhIg_RszBDMab`
- Android deep links: `kaamlink://auth-callback` (legacy/fallback),
  `kaamlink://auth-confirmed`
- Confirmation page: `https://zyklrndmmcmsktagkzek.supabase.co/functions/v1/auth-confirmed`

Never place a service-role key or Management API PAT in the APK,
`KaamLink.csproj`, `appsettings.json`, or any client-side config. Only the
publishable key above is shipped.

## Apply the OTP templates (required once)

Supabase passwordless email uses the "Magic Link or OTP" template. A brand-new
email with `create_user: true` uses the **Confirm signup** template, so both
templates must show `{{ .Token }}` instead of `{{ .ConfirmationURL }}` [3](https://supabase.com/docs/guides/auth/auth-email-passwordless).

The connector **can** set supported auth flags (email enabled, signup enabled,
`mailer_autoconfirm: false`, site URL, redirect allowlist, JWT expiry, refresh
token reuse interval). It **cannot** edit hosted email-template fields. Those
require the Supabase Management API or Dashboard with a scoped PAT.

### Option A — local script (recommended)

1. Create a scoped Management API PAT at
   `https://supabase.com/dashboard/account/tokens` with `auth:write`,
   `auth_config_write` and `project_admin_write`.
2. Do **not** paste the token into chat or commit it.
3. Run from the repository root:

```bash
export SUPABASE_ACCESS_TOKEN=sbp_your_scoped_token_here
python3 tools/configure-supabase-otp-templates.py
```

The script PATCHes `https://api.supabase.com/v1/projects/zyklrndmmcmsktagkzek/config/auth`
and sets:

- `mailer_templates_magic_link_content` and
  `mailer_templates_confirmation_content` to an OTP-code template with
  `{{ .Token }}` and no link.
- `mailer_subjects_magic_link` / `mailer_subjects_confirmation` to
  `Your KaamLink code`.
- `mailer_otp_length: 8`, `mailer_otp_exp: 3600`, `password_min_length: 8`.
- `site_url` to the `auth-confirmed` Edge Function page (no empty click page).
- `uri_allow_list` to both KaamLink deep links + the confirmed page.
- Email signup enabled and `mailer_autoconfirm: false`.

### Option B — Supabase secret + protected helper Edge Function

A helper Edge Function `configure-otp-templates` is deployed on the project. It
is protected by the `OTP_TEMPLATE_ADMIN_TOKEN` secret and reads
`SUPABASE_ACCESS_TOKEN` from Supabase secrets. This is useful when
`api.supabase.com` is blocked from your machine.

1. Supabase Dashboard → Project Settings → Edge Functions → Secrets.
2. Keep the already-created `OTP_TEMPLATE_ADMIN_TOKEN` secret. It is a random
   admin token; do not commit it or paste it into chat.
3. Add secret `SUPABASE_ACCESS_TOKEN` with the scoped PAT from Option A.
4. Invoke the helper once, replacing `ADMIN_TOKEN` with the value of the
   `OTP_TEMPLATE_ADMIN_TOKEN` secret:

```bash
curl -X POST \
  https://zyklrndmmcmsktagkzek.supabase.co/functions/v1/configure-otp-templates \
  -H "Content-Type: application/json" \
  -H "x-admin-token: ADMIN_TOKEN" -d '{}'
```

5. Remove or rotate `SUPABASE_ACCESS_TOKEN` after the templates are applied if
   it is no longer needed. Keep `OTP_TEMPLATE_ADMIN_TOKEN` if you want to reuse
   the helper later.

The helper returns `401` without the admin header, `503` if
`SUPABASE_ACCESS_TOKEN` is missing, and a non-2xx status if the Management API
rejects the PAT.

## Current connector-applied config

Already applied through the Supabase connector on
`kaamlink-mobile-auth` / `zyklrndmmcmsktagkzek`:

- `external_email_enabled: true`
- `disable_signup: false`
- `mailer_autoconfirm: false`
- `external_phone_enabled: false`, `sms_autoconfirm: false`
- `jwt_exp: 3600`
- effective `refresh_token_rotation_enabled: true`,
  `security_refresh_token_reuse_interval: 10`
- `site_url`: the `auth-confirmed` Edge Function URL
- `uri_allow_list`: both `kaamlink://` deep links + the `auth-confirmed` URL
- Deployed Edge Functions: `auth-confirmed` (real HTML page),
  `configure-otp-templates` v2 (protected helper)
- Secrets: `OTP_TEMPLATE_ADMIN_TOKEN` created;
  `SUPABASE_ACCESS_TOKEN` intentionally **not** set

Hosted defaults observed on the new project: `mailer_otp_length: 8`,
`password_min_length: 6`, no custom SMTP. The Management API step above changes
the templates and raises `password_min_length` to 8 to match the app's rule.
Until that step runs the app still accepts 8-digit codes, but the default emails
still contain a click link instead of a code.

## Password rules and security

- Client requires at least 8 characters, one letter and one number, and a
  matching confirmation.
- Supabase `password_min_length` is set to 8 by the template script/helper;
  the hosted default before that step is 6.
- `password_set: true` is stored in `user_metadata`, not in a client-only flag.
- The backend derives `requiresPasswordSetup` from that metadata on every
  exchange.
- Only the public publishable/anon key ships in the mobile app. A service-role
  key or Management API PAT must never be placed in the APK, `KaamLink.csproj`,
  `appsettings.json`, or any client-side config.

## Production checklist

- **Custom SMTP:** Supabase's built-in email service is limited to about 2
  emails/hour, so configure custom SMTP or a Send Email Hook before real users
  rely on OTP [2](https://supabase.com/docs/guides/auth/rate-limits) [4](https://supabase.com/docs/guides/auth/auth-smtp).
- **HTTPS API:** build the Android app with
  `-p:KaamLinkApiBaseUrl=https://your-api.example.com/`.
- **Backend env:** set `Supabase__Url` and `Supabase__AnonKey`; Production
  startup guards reject a missing/half-configured Supabase section.
- **Migration:** apply `20261027000000_Phase06EmailAuth` (`users.email` +
  `ux_users_email`).
- **Redirect allowlist:** keep both `kaamlink://` deep links and the
  `auth-confirmed` URL in Supabase Auth URL configuration.
- **Phone OTP:** remains available, but Production still needs a real SMS
  provider; the default `log` OTP sink is rejected by startup guards.

## Verification performed here

- Supabase connector: new dedicated project created and healthy; auth/db/rest
  healthy; supported auth flags + site URL + allowlist applied; dedicated
  publishable key created; `auth-confirmed` and `configure-otp-templates` Edge
  Functions deployed; `OTP_TEMPLATE_ADMIN_TOKEN` secret created.
- Static gate: `tools/verify-email-auth.py` now runs 135 checks and
  `tools/verify-gates-selftest.py` detects 64 injected defects. They cover OTP
  DTOs, password-set metadata, backend exchange, migration coherence, terms
  gating, localization, aurora palette, dedicated project wiring, helper Edge
  Function protection and docs. `bash tools/verify-all.sh` is green.
- Still required from you: set the scoped `SUPABASE_ACCESS_TOKEN` and run one of
  the template options above, then configure custom SMTP before real users.
