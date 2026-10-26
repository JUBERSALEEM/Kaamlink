#!/usr/bin/env bash
# Run every static gate from the repository root. The Android/.NET toolchain is
# not available in every environment, so this is the repeatable CI-equivalent
# check that cross-file app/backend contracts have not drifted.
set -euo pipefail

cd "$(dirname "$0")/.."

gates=(
  tools/verify-phase03.py
  tools/verify-phase04.py
  tools/verify-phase05.py
  tools/verify-bindings.py
  tools/verify-theme-contrast.py
  tools/verify-ui-redesign.py
  tools/verify-global-phone-directory.py
  tools/verify-compile-surface.py
  tools/verify-connectivity.py
  tools/verify-gates-selftest.py
)

for gate in "${gates[@]}"; do
  echo
  echo "== $gate"
  log="$(mktemp)"
  if python3 "$gate" >"$log" 2>&1; then
    tail -n 3 "$log"
  else
    cat "$log"
    echo "FAILED: $gate" >&2
    rm -f "$log"
    exit 1
  fi
  rm -f "$log"
done

echo
echo "ALL STATIC GATES PASSED"
