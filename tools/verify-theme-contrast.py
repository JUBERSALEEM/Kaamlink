#!/usr/bin/env python3
"""
Theme gate: contrast + key parity for every palette dictionary.

A theme is a palette swap, so two things must hold for every theme:
  1. it declares exactly the same keys as the default palette — a missing token
     renders as an unthemed (often unreadable) screen;
  2. every text/background pair the styles actually use meets WCAG AA
     (4.5:1 for text, 3:1 for non-text UI like input outlines).

Run:  python3 tools/verify-theme-contrast.py [-v]
Exit: 0 = all themes pass, 1 = at least one pair or key set failed.
"""

from __future__ import annotations

import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)

DEFAULT = "Resources/Styles/Colors.xaml"
THEMES = [DEFAULT] + sorted(glob.glob("Resources/Themes/*.xaml"))

# (foreground, background, minimum ratio, why)
PAIRS = [
    ("TextPrimary", "CardSurface", 4.5, "body text on a card"),
    ("TextPrimary", "CardSurfaceTop", 4.5, "body text on the lit top of a card"),
    ("TextPrimary", "CardSurfaceBottom", 4.5, "body text on the shaded base of a card"),
    ("TextPrimary", "CardSurfaceAlt", 4.5, "body text on a quiet surface"),
    ("TextPrimary", "PageBackground", 4.5, "headings on the page"),
    ("TextSecondary", "CardSurface", 4.5, "secondary text on a card"),
    ("TextSecondary", "CardSurfaceBottom", 4.5, "secondary text on the shaded base"),
    ("TextSecondary", "CardSurfaceAlt", 4.5, "secondary text on a quiet surface"),
    ("TextSecondary", "PageBackground", 4.5, "secondary text on the page"),
    ("TextMuted", "CardSurface", 4.5, "captions on a card"),
    ("TextMuted", "CardSurfaceBottom", 4.5, "captions on the shaded base"),
    ("TextMuted", "CardSurfaceAlt", 4.5, "captions on a quiet surface"),
    ("TextMuted", "PageBackground", 4.5, "captions on the page"),
    ("TextOnPrimary", "PrimaryFillTop", 4.5, "button label on the lit top"),
    ("TextOnPrimary", "PrimaryFill", 4.5, "button label on the body"),
    ("TextOnPrimary", "PrimaryFillBottom", 4.5, "button label on the base"),
    ("TextOnPrimary", "AccentFillTop", 4.5, "accent label on the lit top"),
    ("TextOnPrimary", "AccentFillBottom", 4.5, "accent label on the base"),
    ("Error", "ErrorSoft", 4.5, "error text on its banner"),
    ("Warning", "WarningSoft", 4.5, "warning text on its banner"),
    ("Success", "SuccessSoft", 4.5, "success text on its banner"),
    ("Info", "InfoSoft", 4.5, "info text on its banner"),
    ("Primary", "PrimarySoft", 4.5, "brand pill text on its tint"),
    ("Accent", "AccentSoft", 4.5, "accent pill text on its tint"),
    ("BorderStrong", "CardSurface", 3.0, "input outline on a card"),
    ("BorderStrong", "PageBackground", 3.0, "input outline on the page"),
]


def parse(path: str) -> dict[str, str]:
    text = open(path, encoding="utf-8").read()
    return {k: v.upper() for k, v in
            re.findall(r'<Color x:Key="(\w+)">#([0-9A-Fa-f]{6})</Color>', text)}


def all_keys(path: str) -> set[str]:
    return set(re.findall(r'x:Key="(\w+)"', open(path, encoding="utf-8").read()))


def channel(value: float) -> float:
    value /= 255
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def luminance(hex_value: str) -> float:
    r, g, b = (int(hex_value[i:i + 2], 16) for i in (0, 2, 4))
    return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b)


def contrast(a: str, b: str, palette: dict[str, str]) -> float:
    la, lb = luminance(palette[a]), luminance(palette[b])
    high, low = max(la, lb), min(la, lb)
    return (high + 0.05) / (low + 0.05)


def main() -> int:
    verbose = "-v" in sys.argv
    failures: list[str] = []
    base_keys = all_keys(DEFAULT)
    base_palette = parse(DEFAULT)
    checks = 0

    for theme in THEMES:
        name = os.path.basename(theme)
        palette = parse(theme)
        keys = all_keys(theme)

        missing = sorted(base_keys - keys)
        extra = sorted(keys - base_keys)
        checks += 1
        if missing:
            failures.append(f"{name}: missing token(s) {missing}")
        if extra:
            failures.append(f"{name}: token(s) not in the default palette {extra}")

        if theme != DEFAULT:
            # identical structure, not just identical names: same brush kinds
            base_kinds = re.findall(r'<(\w+)\s+x:Key="', open(DEFAULT, encoding="utf-8").read())
            theme_kinds = re.findall(r'<(\w+)\s+x:Key="', open(theme, encoding="utf-8").read())
            checks += 1
            if base_kinds != theme_kinds:
                failures.append(f"{name}: resource kinds/order differ from the default palette")

        for fg, bg, need, why in PAIRS:
            checks += 1
            if fg not in palette or bg not in palette:
                failures.append(f"{name}: pair {fg} on {bg} references an undeclared token")
                continue
            ratio = contrast(fg, bg, palette)
            if ratio < need:
                failures.append(
                    f"{name}: {fg} on {bg} is {ratio:.2f}:1, needs {need}:1 ({why})")
            elif verbose:
                print(f"  ok  {name:22} {fg:16} on {bg:22} {ratio:5.2f}:1  {why}")

    if failures:
        print(f"\n{checks} checks, {len(failures)} failure(s):\n")
        for failure in failures:
            print("  -", failure)
        return 1

    print(f"\n{checks} checks, 0 failures across {len(THEMES)} theme palettes")
    print("ALL THEMES PASS CONTRAST AND PARITY")
    return 0


if __name__ == "__main__":
    sys.exit(main())
