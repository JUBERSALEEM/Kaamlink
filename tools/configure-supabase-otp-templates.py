#!/usr/bin/env python3
"""Apply the hosted Supabase OTP email templates via the Management API.

The connected Supabase tool can read auth config but cannot edit email-template
fields. Hosted templates require a scoped Management API personal access token
(PAT) with auth:write / auth_config_write. Outbound access to api.supabase.com
is blocked in the Arena sandbox, so run this from a trusted machine:

    export SUPABASE_ACCESS_TOKEN=sbp_...   # do not commit this token
    python3 tools/configure-supabase-otp-templates.py

It changes BOTH the "Magic Link or OTP" and "Confirm signup" templates. A new
email with shouldCreateUser=true receives the confirm-signup template, so
changing only the magic-link template would still send a link [3](https://supabase.com/docs/guides/auth/auth-email-passwordless).
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request

DEFAULT_REF = "sdxywcosqidyopbfevoe"
CONFIRMED_URL = "https://sdxywcosqidyopbfevoe.supabase.co/functions/v1/auth-confirmed"
DEEP_LINK = "kaamlink://auth-callback"

OTP_TEMPLATE = """<h2>Your KaamLink verification code</h2>
<p>Enter this one-time code in the KaamLink app to confirm your email address and finish signing in.</p>
<p style="font-size:30px;font-weight:700;letter-spacing:8px;">{{ .Token }}</p>
<p>This code expires shortly and can only be used once. If you did not request it, you can safely ignore this email.</p>""".strip()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project-ref", default=os.environ.get("SUPABASE_PROJECT_REF", DEFAULT_REF))
    ap.add_argument("--site-url", default=CONFIRMED_URL)
    args = ap.parse_args()

    token = os.environ.get("SUPABASE_ACCESS_TOKEN", "").strip()
    if not token:
        print("ERROR: set SUPABASE_ACCESS_TOKEN to a scoped Supabase Management API PAT.", file=sys.stderr)
        print("Create one at https://supabase.com/dashboard/account/tokens", file=sys.stderr)
        print("Do not paste the token into chat or commit it to the repository.", file=sys.stderr)
        return 2

    body = {
        "site_url": args.site_url,
        "uri_allow_list": f"{DEEP_LINK},{args.site_url}",
        "disable_signup": False,
        "external_email_enabled": True,
        "mailer_autoconfirm": False,
        "mailer_otp_length": 6,
        "mailer_otp_exp": 3600,
        "password_min_length": 8,
        "mailer_subjects_magic_link": "Your KaamLink verification code",
        "mailer_subjects_confirmation": "Your KaamLink verification code",
        "mailer_templates_magic_link_content": OTP_TEMPLATE,
        "mailer_templates_confirmation_content": OTP_TEMPLATE,
    }

    url = f"https://api.supabase.com/v1/projects/{args.project_ref}/config/auth"
    request = urllib.request.Request(
        url,
        data=json.dumps(body).encode("utf-8"),
        method="PATCH",
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = response.read().decode("utf-8", errors="replace")
            status = response.status
    except urllib.error.HTTPError as error:
        payload = error.read().decode("utf-8", errors="replace")
        print(f"Management API returned {error.code}: {payload[:800]}", file=sys.stderr)
        return 1
    except OSError as error:
        print(f"Could not reach the Supabase Management API: {error}", file=sys.stderr)
        return 1

    print(f"Supabase auth config updated (HTTP {status}).")
    print(f"Site URL: {args.site_url}")
    print("Templates: magic_link + confirmation now render {{ .Token }} (no link).")
    print("OTP length: 6; password minimum: 8.")
    if payload:
        try:
            parsed = json.loads(payload)
            template = parsed.get("mailer_templates_magic_link_content", "")
            print(f"Magic-link template contains token: {'{{ .Token }}' in template}")
        except json.JSONDecodeError:
            pass
    return 0 if 200 <= status < 300 else 1


if __name__ == "__main__":
    raise SystemExit(main())
