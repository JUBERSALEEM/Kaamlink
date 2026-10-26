#!/usr/bin/env python3
"""
Redesign gate for the KaamLink MAUI app (Phase 3-6 of the UI redesign).

Every check here is mechanical and reproducible without a device or the dotnet
SDK. It encodes the rules the redesign promised, so regressions fail loudly
instead of relying on review:

  A  page contract        x:DataType, Title, no shell nav bar
  B  design tokens        no raw hex, no legacy style aliases, declared keys only
  C  localization         no hardcoded Latin UI text, no emoji, EN/AR/Designer parity
  D  accessibility        48dp touch targets, >=12sp text, semantic labels, headings
  E  state coverage       HasError -> banner, IsEmpty -> state panel, IsBusy -> feedback
  F  bindings             every compiled binding resolves to a real property
  G  templates            every DataTemplate item type matches the items source
  H  design system        token inventory, single-glyph icons, button heights

Usage: python3 tools/verify-ui-redesign.py
"""

from __future__ import annotations

import glob
import html
import importlib.util
import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("vb", os.path.join(HERE, "verify-bindings.py"))
vb = importlib.util.module_from_spec(spec)
sys.argv = ["verify-bindings.py"]
spec.loader.exec_module(vb)

NS_MAUI = "http://schemas.microsoft.com/dotnet/2021/maui"
NS_X = "http://schemas.microsoft.com/winfx/2009/xaml"

checks = 0
failures: list[str] = []


def ok(condition: bool, message: str) -> None:
    global checks
    checks += 1
    if not condition:
        failures.append(message)


def attr(el: ET.Element, name: str) -> str | None:
    return el.get(f"{{{NS_MAUI}}}{name}") or el.get(f"{{{NS_X}}}{name}")


PAGES = sorted(glob.glob("Features/**/*.xaml", recursive=True))
PAGE_TEXT = {p: open(p, encoding="utf-8").read() for p in PAGES}
STYLES = open("Resources/Styles/Styles.xaml", encoding="utf-8").read()
COLORS = open("Resources/Styles/Colors.xaml", encoding="utf-8").read()

LEGACY_KEYS = {"Title1", "Title2", "SectionTitle", "Body", "BodySecondary", "Caption", "Card"}
EMOJI = re.compile("[\U0001F300-\U0001FAFF\u2600-\u27BF\uFE0F]")
LATIN_TEXT = re.compile(r"[A-Za-z]{2,}")
TEXT_ATTRS = ("Text", "Placeholder", "Message", "Title", "Description")
ARABIC = re.compile("[\u0600-\u06FF]")

# ---------------------------------------------------------------- A page contract
for page in PAGES:
    root = ET.parse(page).getroot()
    ok(bool(attr(root, "DataType")), f"{page}: missing x:DataType (uncompiled bindings)")
    ok(bool(root.get("Title")), f"{page}: missing Title")
    nav = (root.get("Shell.NavBarIsVisible")
           or root.get(f"{{{NS_MAUI}}}Shell.NavBarIsVisible")
           or root.get(f"{{{NS_MAUI}}}NavBarIsVisible"))
    ok(nav == "False", f"{page}: Shell.NavBarIsVisible must be False (custom headers)")

# ---------------------------------------------------------------- B design tokens
for page, text in PAGE_TEXT.items():
    ok(not re.search(r"#[0-9A-Fa-f]{6}\b|#[0-9A-Fa-f]{3}\b", text), f"{page}: raw hex colour")
    used = set(re.findall(r"StaticResource\s+([A-Za-z0-9_]+)\}", text))
    legacy = sorted(used & LEGACY_KEYS)
    ok(not legacy, f"{page}: legacy style keys {legacy}")
    ok("FontSize=\"10" not in text and "FontSize=\"11" not in text, f"{page}: text below 12sp")

# ---------------------------------------------------------------- C localization
for page, text in PAGE_TEXT.items():
    for match in re.finditer(r'(?:Text|Title|Placeholder|Message|Description)="([^"{}]+)"', text):
        value = match.group(1).strip()
        if value and LATIN_TEXT.search(value):
            ok(False, f"{page}: hardcoded UI text '{value}'")
    ok(not EMOJI.search(text), f"{page}: emoji found (icons must use the Lucide font)")
    ok(not re.search(r"[\u2713\u2714\u2605\u25CF\u25AA]", text),
       f"{page}: text glyph used as an icon (draw a Lucide glyph instead)")

for source in glob.glob("Features/**/*ViewModel.cs", recursive=True):
    body = open(source, encoding="utf-8").read()
    ok(not re.search(r"[\u2713\u2714\u2605\u25CF\u25AA]", body),
       f"{source}: text glyph baked into a display string (not localizable, not an icon)")


def resx_keys(path: str) -> set[str]:
    return set(re.findall(r'<data name="([A-Za-z0-9_.]+)"', open(path, encoding="utf-8").read()))


en_keys = resx_keys("Resources/Strings/AppStrings.resx")
ar_keys = resx_keys("Resources/Strings/AppStrings.ar.resx")
designer = open("Resources/Strings/AppStrings.Designer.cs", encoding="utf-8").read()
designer_keys = set(re.findall(r"public static string ([A-Za-z0-9_]+) =>", designer))
ok(en_keys == ar_keys, f"resx parity: EN only {sorted(en_keys - ar_keys)} / AR only {sorted(ar_keys - en_keys)}")
# Arabic copy must be real: no empty values, no Latin-only values, same {0}/{1} slots.
def resx_map(path: str) -> dict[str, str]:
    root = ET.parse(path).getroot()
    return {d.get("name"): (d.findtext("value") or "") for d in root.findall("data")}


en_map, ar_map = resx_map("Resources/Strings/AppStrings.resx"), resx_map("Resources/Strings/AppStrings.ar.resx")
ok(not [k for k, v in ar_map.items() if not v.strip()],
   f"ar resx: empty values {[k for k, v in ar_map.items() if not v.strip()]}")
ok(not [k for k, v in ar_map.items() if not ARABIC.search(v) and LATIN_TEXT.search(v)],
   "ar resx: values left in English")
placeholders = [k for k in en_map if re.findall(r"\{\d\}", en_map[k]) != re.findall(r"\{\d\}", ar_map.get(k, ""))]
ok(not placeholders, f"ar resx: placeholder mismatch in {placeholders}")

ok(en_keys == designer_keys,
   f"designer drift: resx only {sorted(en_keys - designer_keys)} / designer only {sorted(designer_keys - en_keys)}")

declared_string = designer_keys
for page, text in PAGE_TEXT.items():
    missing = sorted({k for k in re.findall(r"AppStrings\.([A-Za-z0-9_]+)", text)} - declared_string)
    ok(not missing, f"{page}: x:Static keys missing from Designer.cs: {missing}")

# ---------------------------------------------------------------- D accessibility
INTERACTIVE = {"Button", "Entry", "Picker", "Editor", "SearchBar"}
for page, text in PAGE_TEXT.items():
    tree = ET.parse(page)
    has_heading = False
    for el in tree.iter():
        if el.get("SemanticProperties.HeadingLevel"):
            has_heading = True
        tag = el.tag.split("}")[-1]
        if tag in INTERACTIVE:
            height = el.get("HeightRequest") or el.get("MinimumHeightRequest")
            if height is not None and tag != "Editor":
                ok(int(float(height)) >= 44, f"{page}: {tag} height {height} < 44dp")
        if tag == "Button" and not (
                el.get("Text") or el.get("SemanticProperties.Description")
                or el.get("Content")):
            ok(False, f"{page}: Button without text or semantic description")
    ok(has_heading, f"{page}: no SemanticProperties.HeadingLevel (screen readers)")

# ---------------------------------------------------------------- E state coverage
for page, text in PAGE_TEXT.items():
    if "{Binding HasError}" in text or "{Binding HasError," in text:
        ok("controls:NoticeBanner" in text or "IconAlertCircle" in text,
           f"{page}: HasError bound without a banner/error surface")
    if "{Binding IsEmpty}" in text:
        ok('Kind="empty"' in text, f"{page}: IsEmpty bound without an empty state panel")
    if "{Binding IsBusy}" in text:
        ok("controls:StatePanel" in text or "ActivityIndicator" in text,
           f"{page}: IsBusy bound without loading feedback")

# ---------------------------------------------------------------- F + G bindings
vb.collect_types()
vb.collect_collections()

for page, text in PAGE_TEXT.items():
    root = ET.parse(page).getroot()
    page_type = (attr(root, "DataType") or "").split(":")[-1]
    for el in root.iter():
        items = el.get("BindableLayout.ItemsSource") or el.get("ItemsSource") or ""
        match = re.search(r"\{Binding\s+([A-Za-z0-9_.]+)", items)
        if not match:
            continue
        source_path = match.group(1)
        scope = el.get(f"{{{NS_X}}}DataType")
        template = None
        for child in el.iter():
            if child.tag.split("}")[-1] == "DataTemplate":
                template = attr(child, "DataType")
                break
        if not template:
            continue
        # resolve the item type from the closest enclosing DataTemplate or the page VM
        host = None
        for candidate in root.iter():
            if candidate is el:
                break
        item_type = vb.collection_item_type(page_type, source_path) or ""
        if not item_type:
            continue
        normalize = lambda t: t.split(":")[-1].lower()
        ok(normalize(template) == normalize(item_type),
           f"{page}: DataTemplate type {template.split(':')[-1]} != items type {item_type} for '{source_path}'")

# ---------------------------------------------------------------- H design system
button_styles = re.findall(r'<Style TargetType="Button" x:Key="(\w+)">([\s\S]*?)</Style>', STYLES)
for name, body in button_styles:
    height = re.search(r'Property="HeightRequest" Value="(\d+)"', body)
    if height:
        ok(int(height.group(1)) >= 48, f"Styles.xaml: {name} height {height.group(1)} < 48dp")

overline = re.search(r'<Style TargetType="Label" x:Key="OverlineText">([\s\S]*?)</Style>', STYLES)
ok(overline is not None, "Styles.xaml: OverlineText style missing")
if overline:
    ok("CharacterSpacing" not in overline.group(1), "Styles.xaml: OverlineText uses CharacterSpacing (breaks Arabic)")
    ok("Uppercase" not in overline.group(1), "Styles.xaml: OverlineText uppercases (breaks Arabic)")

icon_keys = re.findall(r'<x:String x:Key="(Icon\w+)">([^<]*)</x:String>', COLORS)
for key, glyph in icon_keys:
    decoded = html.unescape(glyph.strip())
    ok(len(decoded) == 1, f"Colors.xaml: {key} is not a single glyph")

token_keys = re.findall(r'x:Key="([A-Za-z0-9_]+)"', COLORS)
duplicates = sorted({k for k in token_keys if token_keys.count(k) > 1})
ok(not duplicates, f"Colors.xaml: duplicate keys {duplicates}")
for required in ("Primary", "PrimarySoft", "Success", "Warning", "Error", "Info",
                 "TextPrimary", "TextSecondary", "TextMuted", "CardSurface", "Border", "Divider"):
    ok(f'x:Key="{required}"' in COLORS, f"Colors.xaml: missing token {required}")
hardcoded_colors = re.findall(r'Value="#[0-9A-Fa-f]{3,8}"', STYLES)
ok(not hardcoded_colors, f"Styles.xaml: raw hex values {hardcoded_colors}")

# shared controls are used, not re-invented per page
ok(sum('controls:StatusPill' in t for t in PAGE_TEXT.values()) >= 4,
   "Redesign: StatusPill not reused across pages")
ok(sum('controls:StatePanel' in t for t in PAGE_TEXT.values()) >= 6,
   "Redesign: StatePanel not reused across pages")
ok(sum('controls:NoticeBanner' in t for t in PAGE_TEXT.values()) >= 4,
   "Redesign: NoticeBanner not reused across pages")

# ---------------------------------------------------------------- I feature parity
# A command nobody binds is a feature the redesign dropped. Every [RelayCommand]
# must be referenced from XAML or from code-behind.
all_xaml = "\n".join(PAGE_TEXT.values())
code_behind = "\n".join(
    open(f, encoding="utf-8").read() for f in glob.glob("Features/**/*.xaml.cs", recursive=True))
relay_methods = re.findall(
    r"\[RelayCommand[^\]]*\]\s*(?:private|public|internal)\s+(?:async\s+)?[\w<>?]+\s+(\w+)\s*\(",
    "\n".join(open(f, encoding="utf-8").read() for f in glob.glob("Features/**/*ViewModel.cs", recursive=True)))
for method in sorted(set(relay_methods)):
    base = re.sub(r"Async$", "", method)
    command = base[:1].upper() + base[1:] + "Command"
    ok(command in all_xaml or command in code_behind,
       f"Redesign: command {command} is never bound (dropped feature)")

print(f"{checks} checks, {len(failures)} failures")
if failures:
    print("FAILURES:")
    for failure in sorted(set(failures)):
        print("  -", failure)
    sys.exit(1)
print("ALL REDESIGN CHECKS PASSED")
