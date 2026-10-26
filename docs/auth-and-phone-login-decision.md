# KaamLink sign-in recommendation and rollout prerequisites

**Decision for this implementation:** preserve KaamLink's verified phone as the account's mandatory primary identity. Treat Google as a linked sign-in method, not an email-only account. On first use, the user must verify the phone by the existing OTP flow before a Google identity is linked; returning Google users authenticate through their already-linked identity. Never link accounts by email string alone.

This is the senior-developer recommendation for the current backend: `User.PhoneNumber` is required, E.164 is the identity key, and OTP is the implemented authentication boundary. Google-only accounts would change the domain/account-creation and recovery rules, duplicate detection, backend APIs, and data migration. The user has not asked for that model; the safer assumption is to keep verified phone ownership and add Google as convenience.

## Google flow to implement when credentials/hosting exist

1. On Android, use Google's current Credential Manager Sign in with Google integration, requesting an ID token with a cryptographically random, one-time nonce. Do not use the deprecated legacy Google Sign-In API or a WebView.
2. Send the ID token to the KaamLink API over HTTPS. The server verifies signature, issuer, expiry, intended audience/client ID, and nonce, then identifies the account by Google's immutable `sub` claim.
3. For an unlinked identity, require successful KaamLink OTP phone verification before creating the phone-primary account and linking the Google `sub`. For a linked identity, issue KaamLink tokens through the normal session contract.
4. Keep Google UI unavailable until both provider verification and server-side account linking are deployed; a client-only “sign in” is not authentication.

Google sign-in itself can be a no-cost provider flow, but it still needs an OAuth project/client configuration and secure server-side validation. A public API endpoint must be HTTPS and reachable from the device. Client IDs are identifiers, not secrets; private keys/client secrets must never be shipped in the APK.

## OTP cost and reachability

- The current `Otp:Provider=log` development provider costs nothing because it prints the code in server logs. It is strictly for development and is not a deliverable SMS channel.
- There is no dependable, unlimited, globally free production SMS OTP service. Telecom delivery has a per-message cost and abuse/rate-limit obligations. Do not promise free worldwide SMS.
- Firebase Phone Auth is not a free production SMS shortcut: Google's current pricing says phone authentication is billed per SMS. Firebase phone test numbers are for development only.
- Firebase Phone Number Verification avoids an SMS but is a billed, carrier/SIM-dependent product; its published supported-carrier list as of 2026-10-26 does not include the UAE. It is not a suitable global UAE default or a zero-cost replacement here.
- Keep the current API OTP contract and choose a provider only after deployment region, message cost, sender registration, abuse controls, and supported destinations are known. The app should not call an SMS vendor directly.

## Required before end-to-end mobile auth can pass

- A publicly reachable HTTPS KaamLink API base URL for mobile data/other Wi-Fi. Current Debug URLs are emulator/USB-development routes; Release remains `.invalid`.
- A production server-side OTP provider and secure server credentials for actual SMS.
- A Google Cloud OAuth project and Android + server/web client configuration, plus the backend token-validation/link endpoint. Do not put a client secret in mobile config.

## Sources reviewed (current as of 2026-10-26)

- [Google Credential Manager for Android](https://developers.google.com/identity/android-credential-manager) — current platform credential surface, including Sign in with Google.
- [Google Sign in with Google implementation](https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation) — obtain an ID token and validate it on the relying-party server.
- [Google best practices](https://developers.google.com/identity/siwg/best-practices) — use Credential Manager for standard Android apps.
- [Google server-side ID-token authentication](https://developers.google.com/identity/sign-in/android/backend-auth) — server checks token authenticity, audience, issuer, and expiry.
- [.NET MAUI WebAuthenticator](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/communication/authentication?view=net-maui-10.0) — available browser-based callback API; not a substitute for server token verification.
- [Firebase pricing](https://firebase.google.com/pricing) — Phone Auth is billed per SMS; do not rely on a zero-cost production SMS quota.
- [Firebase Phone Number Verification availability/pricing](https://firebase.google.com/docs/phone-number-verification/pricing) — carrier coverage is partial and production use requires billing.
- [Firebase Phone Number Verification overview](https://firebase.google.com/docs/phone-number-verification) — SIM/carrier verification, with consent and compatibility checks.
