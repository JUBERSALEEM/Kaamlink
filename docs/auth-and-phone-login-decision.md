# KaamLink sign-in recommendation and rollout prerequisites

**Current implementation:** KaamLink supports two real authentication paths:

1. **Phone OTP** accounts keep a verified E.164 phone as the primary identity.
2. **Supabase email OTP + password** accounts use a confirmed email as the primary
   identity. The backend verifies the Supabase access token with GoTrue before
   issuing KaamLink's own JWT/session. Email-only accounts receive a synthetic,
   non-dialable `+999...` phone value so the existing unique `phone_number`
   constraint stays intact without pretending a phone was verified.

Google remains a **future linked sign-in method**, not an implemented login. The
greyed-out button is intentional: there is no Google Cloud OAuth client and no
server-side ID-token verification/account linking. A client-only “sign in with
Google” would be fake authentication.

## Implemented now: free/easy email OTP + password sign-in

- Supabase project `kaamlink-magic-link` (`sdxywcosqidyopbfevoe`) with email auth
  enabled, signup enabled, and both `kaamlink://auth-callback` and the
  `auth-confirmed` Edge Function URL allowlisted.
- Android deep link `kaamlink://auth-callback` remains as a legacy fallback.
- App calls `POST /auth/v1/otp` with `create_user: true`; only the public
  publishable/anon key ships in the APK.
- Supabase emails a 6–8 digit one-time code (no click link). The user types it in
  the app; `POST /auth/v1/verify` returns a Supabase session.
- The session access token is exchanged at `POST /api/v1/auth/email/exchange`.
- Backend `SupabaseUserVerifier` calls GoTrue `/auth/v1/user`, requires
  `email_confirmed_at`, and reads `user_metadata.password_set`.
- `AuthService.ExchangeSupabaseTokenAsync` maps/creates the KaamLink user, issues
  the normal rotating access/refresh token pair, and returns
  `requiresPasswordSetup` when no password exists yet.
- The app then shows **Set a password for next time**; `PUT /auth/v1/user` saves
  the password and `data.password_set = true`. Users may skip and keep using OTP.
- Later sessions can use `POST /auth/v1/token?grant_type=password` with email +
  password, then the same KaamLink exchange endpoint.
- A tickable Terms and Conditions checkbox gates every path; acceptance is
  persisted so the email-app round trip works.

Full setup, exact Management API steps and production checklist:
[`email-magic-link-setup.md`](email-magic-link-setup.md).

Supabase's built-in email provider is free but rate-limited (as low as two
emails/hour). Configure **custom SMTP** or a Send Email Hook before real users
depend on OTP. A service-role key must never be placed in the mobile app.

## Google flow to implement when credentials/hosting exist

1. On Android, use Google's current Credential Manager Sign in with Google integration, requesting an ID token with a cryptographically random, one-time nonce. Do not use the deprecated legacy Google Sign-In API or a WebView.
2. Send the ID token to the KaamLink API over HTTPS. The server verifies signature, issuer, expiry, intended audience/client ID, and nonce, then identifies the account by Google's immutable `sub` claim.
3. For an unlinked identity, require successful KaamLink OTP phone verification (or an equivalent verified-identity exchange) before linking the Google `sub`. For a linked identity, issue KaamLink tokens through the normal session contract. Never link accounts by email string alone.
4. Keep Google UI unavailable until both provider verification and server-side account linking are deployed; a client-only “sign in” is not authentication.

Google sign-in itself can be a no-cost provider flow, but it still needs an OAuth project/client configuration and secure server-side validation. A public API endpoint must be HTTPS and reachable from the device. Client IDs are identifiers, not secrets; private keys/client secrets must never be shipped in the APK.

## OTP cost and reachability

- The current `Otp:Provider=log` development provider costs nothing because it prints the code in server logs. It is strictly for development and is not a deliverable SMS channel.
- There is no dependable, unlimited, globally free production SMS OTP service. Telecom delivery has a per-message cost and abuse/rate-limit obligations. Do not promise free worldwide SMS.
- Firebase Phone Auth is not a free production SMS shortcut: Google's current pricing says phone authentication is billed per SMS. Firebase phone test numbers are for development only.
- Firebase Phone Number Verification avoids an SMS but is a billed, carrier/SIM-dependent product; its published supported-carrier list as of 2026-10-26 does not include the UAE. It is not a suitable global UAE default or a zero-cost replacement here.
- Keep the current API OTP contract and choose a provider only after deployment region, message cost, sender registration, abuse controls, and supported destinations are known. The app should not call an SMS vendor directly.

## Required before end-to-end mobile auth can pass

- A publicly reachable HTTPS KaamLink API base URL supplied at build time with
  `-p:KaamLinkApiBaseUrl=https://.../`. Debug URLs are emulator/USB-development
  routes; an unconfigured Release build now shows a clear setup error.
- The Phase 06 database migration applied (`users.email` + `ux_users_email`).
- `Supabase__Url` and `Supabase__AnonKey` set in the deployed API; Production
  startup guards reject a missing/half-configured Supabase section.
- Run `tools/configure-supabase-otp-templates.py` once with a scoped Management
  API PAT so both Supabase email templates render `{{ .Token }}` instead of a link.
- Custom SMTP in Supabase for production OTP email delivery.
- A production server-side OTP provider and secure server credentials for actual
  SMS, if phone OTP is offered to real users.
- For Google only: a Google Cloud OAuth project and Android + server/web client
  configuration, plus the backend token-validation/link endpoint. Do not put a
  client secret in mobile config.

## Sources reviewed (current as of 2026-10-27)

- [Supabase passwordless email sign-in](https://supabase.com/docs/guides/auth/auth-email-passwordless) — email OTP / magic-link behavior and redirect allowlists.
- [Supabase email templates](https://supabase.com/docs/guides/auth/auth-email-templates) — `{{ .Token }}` and hosted template variables.
- [Supabase Management API auth config](https://supabase.com/docs/reference/api/v1-update-auth-service-config) — template and OTP-length fields.
- [Supabase Auth rate limits](https://supabase.com/docs/guides/auth/rate-limits) and [custom SMTP](https://supabase.com/docs/guides/auth/auth-smtp) — built-in email limits and production SMTP.
- [Google Credential Manager for Android](https://developers.google.com/identity/android-credential-manager) — current platform credential surface, including Sign in with Google.
- [Google Sign in with Google implementation](https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation) — obtain an ID token and validate it on the relying-party server.
- [Google best practices](https://developers.google.com/identity/siwg/best-practices) — use Credential Manager for standard Android apps.
- [Google server-side ID-token authentication](https://developers.google.com/identity/sign-in/android/backend-auth) — server checks token authenticity, audience, issuer, and expiry.
- [.NET MAUI WebAuthenticator](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/communication/authentication?view=net-maui-10.0) — available browser-based callback API; not a substitute for server token verification.
- [Firebase pricing](https://firebase.google.com/pricing) — Phone Auth is billed per SMS; do not rely on a zero-cost production SMS quota.
- [Firebase Phone Number Verification availability/pricing](https://firebase.google.com/docs/phone-number-verification/pricing) — carrier coverage is partial and production use requires billing.
- [Firebase Phone Number Verification overview](https://firebase.google.com/docs/phone-number-verification) — SIM/carrier verification, with consent and compatibility checks.
