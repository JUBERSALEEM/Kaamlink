# Supabase email magic-link signup/login

KaamLink now has a free, passwordless signup/login path: **email magic link**.
It is real authentication, not a fake Google button. The mobile app asks Supabase
GoTrue to email a one-time sign-in link; the link opens the Android app through a
deep link; the app sends the verified Supabase access token to the KaamLink API;
the API verifies it with Supabase and issues KaamLink's own access/refresh token
pair.

> Google sign-in is still disabled. It needs a Google Cloud OAuth client, redirect
> configuration, and server-side ID-token verification/account linking. Those do
> not exist yet, so enabling the button would have been a fake login.

## What is configured

- Supabase project: `kaamlink-magic-link`
- Project ref: `sdxywcosqidyopbfevoe`
- Public URL: `https://sdxywcosqidyopbfevoe.supabase.co`
- Auth: email enabled, signup enabled, email confirmation required.
- Redirect allowlist / site URL: `kaamlink://auth-callback`
- Android deep link: `kaamlink://auth-callback` (see
  `Platforms/Android/MainActivity.cs`).
- Android `OnCreate` / `OnNewIntent` VIEW intents are forwarded by
  `MauiProgram.ConfigureLifecycleEvents` to `App.OnAppLinkRequestReceived`;
  only the known callback host is accepted.
- App build defaults: `KaamLinkSupabaseUrl` and `KaamLinkSupabaseAnonKey` in
  `KaamLink.csproj`.
- Backend config: `Supabase:Url` and `Supabase:AnonKey` in
  `backend/src/KaamLink.Api/appsettings.json`, overridable with
  `Supabase__Url` / `Supabase__AnonKey`.

Only the **publishable/anon** key is shipped. A service-role key must never be
placed in the mobile app, the client `csproj`, or any client-side config. The
backend uses the public key only to call GoTrue's `/auth/v1/user`; it does not
use a database service role.

## Flow

1. User ticks the Terms and Conditions checkbox on the login screen.
2. User enters an email and taps **Email me a sign-in link**.
3. App `POST`s to `https://<project>.supabase.co/auth/v1/otp` with
   `{ email, create_user: true, email_redirect_to: "kaamlink://auth-callback" }`.
4. Supabase emails a magic link. Opening it on the device starts/resumes the app
   with tokens in the URL fragment:
   `kaamlink://auth-callback#access_token=...&refresh_token=...&type=magiclink`.
5. `App.OnAppLinkRequestReceived` stores the callback and routes to `//login`.
6. `LoginViewModel` sends the access token to
   `POST /api/v1/auth/email/exchange`.
7. `SupabaseUserVerifier` calls GoTrue `/auth/v1/user` with that token and the
   public `apikey`, and requires a confirmed email. It never trusts a
   client-supplied email.
8. `AuthService` finds or creates a KaamLink `User` by the confirmed email,
   assigns a synthetic non-dialable `+999...` phone identity for email-only
   accounts, and issues the normal KaamLink JWT + rotating refresh token.
9. `SessionService.CompleteLoginAsync` stores the KaamLink session and the app
   navigates using the server's own role summary.

Supabase access tokens are **not** accepted directly by KaamLink API endpoints.
The exchange is mandatory.

## Database change

Migration `20261027000000_Phase06EmailAuth` adds nullable `users.email` and a
unique index `ux_users_email`. Existing phone-only accounts keep `NULL`;
PostgreSQL allows multiple NULLs in a unique index. Email-only accounts get a
deterministic `+999` E.164-shaped phone derived from the Supabase subject, so
`users.phone_number` stays unique without pretending the user verified a real
phone number.

## Production checklist

- **Custom SMTP:** Supabase's built-in email provider is rate-limited (as low as
  two emails/hour). Configure custom SMTP or a Send Email Hook in the Supabase
  dashboard before real users rely on magic links.
- **HTTPS API:** build the Android app with
  `-p:KaamLinkApiBaseUrl=https://your-api.example.com/`. The exchange endpoint
  and every other API call use that base URL. Without it, Release builds show a
  clear "server not configured" state instead of a misleading DNS error.
- **Backend environment:** set `Supabase__Url` (HTTPS) and
  `Supabase__AnonKey` in the deployed API. Production startup guards reject a
  missing or half-configured Supabase section.
- **Migrations:** `Database:ApplyMigrationsOnStartup` or `dotnet ef database
  update` must apply the Phase 06 migration before the first exchange.
- **Redirect allowlist:** keep `kaamlink://auth-callback` in Supabase Auth URL
  configuration. If the app ID/scheme changes, update both the Android intent
  filter and the allowlist.
- **Phone OTP:** remains available, but Production still needs a real SMS
  provider; the default `log` OTP sink is rejected by startup guards.

## Verification performed here

- Supabase connector: project created and healthy; email auth enabled; redirect
  allowlist set to `kaamlink://auth-callback`.
- Static gate: `tools/verify-email-auth.py` checks build metadata, deep-link
  parsing, DI, DTOs, backend exchange/verification, migration coherence, terms
  gating, localization, aurora palette and docs.
- `bash tools/verify-all.sh` runs all static gates.

Not performed in this environment: a .NET/Android build, XamlC compilation,
backend startup, Supabase email delivery, a real device deep-link round trip, or
production SMS/SMTP delivery. Those need a build-capable machine and a deployed
HTTPS API.
