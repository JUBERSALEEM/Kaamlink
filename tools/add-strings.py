#!/usr/bin/env python3
"""
Localization helper — keeps the three string sources in lock-step.

The app commits a hand-maintained AppStrings.Designer.cs because ResXFileCodeGenerator
does not run in CLI builds (see the header of that file). That makes adding a string a
three-file edit, which is exactly the kind of thing that drifts. This tool makes the
edit atomic and verifiable.

Usage:
    python3 tools/add-strings.py strings.json          # add/update keys in all 3 files
    python3 tools/add-strings.py strings.json --remove KeyOne KeyTwo
    python3 tools/add-strings.py --check                # report drift, exit 1 on mismatch

strings.json:
    [
      {"key": "WelcomeHeadline", "en": "...", "ar": "..."},
      ...
    ]

Rules enforced by the tool:
  * EN and AR must provide the same keys.
  * New keys are appended (designer properties are appended in the same order).
  * --remove deletes from all three files.
  * --check verifies resx(en) == resx(ar) == designer property set.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parent.parent
EN = ROOT / "Resources/Strings/AppStrings.resx"
AR = ROOT / "Resources/Strings/AppStrings.ar.resx"
DESIGNER = ROOT / "Resources/Strings/AppStrings.Designer.cs"


def resx_keys(text: str) -> list[str]:
    return re.findall(r'<data name="([^"]+)"', text)


def designer_keys(text: str) -> list[str]:
    return re.findall(r"public static string (\w+) =>", text)


def check() -> int:
    en, ar, dz = EN.read_text(encoding="utf-8"), AR.read_text(encoding="utf-8"), DESIGNER.read_text(encoding="utf-8")
    k_en, k_ar, k_dz = set(resx_keys(en)), set(resx_keys(ar)), set(designer_keys(dz))
    ok = True
    if k_en != k_ar:
        ok = False
        print(f"EN-only: {sorted(k_en - k_ar)}\nAR-only: {sorted(k_ar - k_en)}")
    if k_en != k_dz:
        ok = False
        print(f"resx-only: {sorted(k_en - k_dz)}\ndesigner-only: {sorted(k_dz - k_en)}")
    print(f"{'OK' if ok else 'DRIFT'} — en={len(k_en)} ar={len(k_ar)} designer={len(k_dz)}")
    return 0 if ok else 1


def remove(keys: list[str]) -> None:
    for path in (EN, AR):
        text = path.read_text(encoding="utf-8")
        for key in keys:
            text = re.sub(r'\n  <data name="' + re.escape(key) + r'" xml:space="preserve">\n    <value>.*?</value>\n  </data>\n',
                          "\n", text, flags=re.S)
        path.write_text(text, encoding="utf-8")
    dz = DESIGNER.read_text(encoding="utf-8")
    for key in keys:
        dz = re.sub(r"\n        public static string " + re.escape(key) + r" => [^\n]+\n", "\n", dz)
    DESIGNER.write_text(dz, encoding="utf-8")
    print(f"removed {len(keys)} key(s)")


def add(entries: list[dict]) -> None:
    en_text = EN.read_text(encoding="utf-8")
    ar_text = AR.read_text(encoding="utf-8")
    dz_text = DESIGNER.read_text(encoding="utf-8")

    if len({e["key"] for e in entries}) != len(entries):
        sys.exit("duplicate keys in input")
    missing = [e["key"] for e in entries if not e.get("en") or not e.get("ar")]
    if missing:
        sys.exit(f"entries without en+ar: {missing}")

    existing_en = set(resx_keys(en_text))
    existing_ar = set(resx_keys(ar_text))
    existing_dz = set(designer_keys(dz_text))

    en_blocks, ar_blocks, dz_props = [], [], []
    updated = 0
    for entry in entries:
        key, en, ar = entry["key"], entry["en"], entry["ar"]
        present = (key in existing_en, key in existing_ar, key in existing_dz)
        if any(present) and not all(present):
            sys.exit(f"partial key {key} present: en/ar/designer={present} — fix manually first")
        if all(present):
            en_text = re.sub(r'(<data name="' + key + r'" xml:space="preserve">\s*<value>).*?(</value>)',
                             lambda m: m.group(1) + escape(en) + m.group(2), en_text, flags=re.S)
            ar_text = re.sub(r'(<data name="' + key + r'" xml:space="preserve">\s*<value>).*?(</value>)',
                             lambda m: m.group(1) + escape(ar) + m.group(2), ar_text, flags=re.S)
            updated += 1
            continue
        en_blocks.append(f'  <data name="{key}" xml:space="preserve">\n    <value>{escape(en)}</value>\n  </data>\n')
        ar_blocks.append(f'  <data name="{key}" xml:space="preserve">\n    <value>{escape(ar)}</value>\n  </data>\n')
        dz_props.append(
            f"        public static string {key} => ResourceManager.GetString(nameof({key}), Culture)!;\n")

    if en_blocks:
        en_text = en_text.replace("</root>", "".join(en_blocks) + "</root>")
        ar_text = ar_text.replace("</root>", "".join(ar_blocks) + "</root>")
        marker = "    }\n}"
        idx = dz_text.rfind(marker)
        if idx == -1:
            sys.exit("could not locate designer class end")
        dz_text = dz_text[:idx] + "".join(dz_props) + dz_text[idx:]

    EN.write_text(en_text, encoding="utf-8")
    AR.write_text(ar_text, encoding="utf-8")
    DESIGNER.write_text(dz_text, encoding="utf-8")
    print(f"added {len(en_blocks)} key(s), updated {updated}")


def main() -> None:
    args = sys.argv[1:]
    if not args:
        sys.exit(__doc__)
    if args[0] == "--check":
        sys.exit(check())
    payload = json.loads(Path(args[0]).read_text(encoding="utf-8"))
    if "--remove" in args:
        remove(payload.get("remove", []))
    add(payload["add"])
    sys.exit(check())


if __name__ == "__main__":
    main()
