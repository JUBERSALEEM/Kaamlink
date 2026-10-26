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
# Glyphs that stand in for icons: literal (✓ ✔ ★ ● ▪), C# escapes (\u2605) and
# XAML numeric entities (&#x2605;). Escapes render identically at runtime, so
# scanning only the literal characters would let them through.
_GLYPHS = "2713 2714 2605 25CF 25AA".split()
_BS = chr(92)  # regex-escaped backslash, built without a literal escape in source
GLYPH_PATTERN = re.compile(
    "[" + "".join(chr(int(code, 16)) for code in _GLYPHS) + "]"          # literal glyph
    + "|" + _BS + _BS + "u(?:" + "|".join(_GLYPHS) + ")"                 # C# \u2605
    + "|&#[xX](?:" + "|".join(_GLYPHS) + ");")                           # XAML &#x2605;
LATIN_TEXT = re.compile(r"[A-Za-z]{2,}")
TEXT_ATTRS = ("Text", "Placeholder", "Message", "Title", "Description")
ARABIC = re.compile("[\u0600-\u06FF]")

def read(path: str) -> str:
    return open(path, encoding="utf-8").read()


# ------------------------------------------------- A1 escaped query round trip
# Shell decodes query values only for QueryPropertyAttribute; pages that implement
# IQueryAttributable receive the raw text. Every value a route escapes with
# Uri.EscapeDataString must therefore be unescaped by the page that reads it.
SOURCES = sorted(glob.glob("Features/**/*.cs", recursive=True)
                 + glob.glob("Core/**/*.cs", recursive=True))
SOURCE_TEXT = {path: read(path) for path in SOURCES}

escaped_query_keys = set()
for text in SOURCE_TEXT.values():
    escaped_query_keys |= set(re.findall(r"[?&](\w+)=\{Uri\.EscapeDataString\(", text))

for key in sorted(escaped_query_keys):
    for path, text in SOURCE_TEXT.items():
        for match in re.finditer(r'query\.TryGetValue\("' + re.escape(key) + r'"(.*?);',
                                 text, re.S):
            ok("Uri.UnescapeDataString" in match.group(1),
               f"{path}: query value '{key}' is escaped into the route but never decoded "
               f"on receipt (Shell only decodes QueryPropertyAttribute values)")

# ------------------------------------------------------------ A0 typed credentials
# Login/OTP are the only places a user types a number that locks them out if it is
# rejected. Arabic-locale numeric keyboards emit Arabic-Indic digits (٠١٢٣...),
# which int.TryParse / ASCII digit checks refuse - so both entry points must pass
# typed input through DigitText.Normalize before validating or sending it.
PHONE_INPUT = read("Core/Text/PhoneInput.cs")
OTP_VM = read("Features/Auth/OtpViewModel.cs")

ok(os.path.exists("Core/Text/DigitText.cs"),
   "DigitText helper is missing (Arabic-Indic numerals cannot be normalized)")
ok("DigitText.Normalize" in PHONE_INPUT,
   "PhoneInput does not normalize Arabic-Indic digits (Arabic users are told their "
   "valid phone number is invalid)")
ok("DigitText.Normalize" in OTP_VM,
   "OtpViewModel does not normalize Arabic-Indic digits (a valid code is submitted "
   "as non-ASCII and rejected)")
ok("char.IsAsciiDigit" in OTP_VM or "char.IsDigit" not in OTP_VM,
   "OtpViewModel validates digits with char.IsDigit, which accepts Arabic-Indic "
   "digits the API rejects")

# ---------------------------------------------------------------- A page contract
for page in PAGES:
    root = ET.parse(page).getroot()
    page_data_type = (attr(root, "DataType") or "").strip()
    # "{x:Null}" is a non-empty string but explicitly opts OUT of compiled bindings
    # for the whole page, so a truthiness check alone would silently pass.
    ok(bool(page_data_type) and page_data_type != "{x:Null}" and ":" in page_data_type,
       f"{page}: page x:DataType must name a type (got '{page_data_type}') - "
       f"compiled bindings are disabled otherwise")
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
    ok(not GLYPH_PATTERN.search(text),
       f"{page}: text glyph used as an icon (draw a Lucide glyph instead)")

for source in glob.glob("Features/**/*.cs", recursive=True):
    if source.startswith(("obj/", "bin/")) or "Designer" in source:
        continue
    body = open(source, encoding="utf-8").read()
    ok(not GLYPH_PATTERN.search(body),
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

# ---------------------------------------------------------------- J markup hazards
# x:Reference inside a DataTemplate depends on XAML name scopes, which do not cross
# the template boundary reliably (and .NET 10 broke explicit-source bindings in
# CollectionView item templates). Reach the page VM with RelativeSource instead.
for page in PAGES:
    root = ET.parse(page).getroot()

    def walk_templates(el, inside=False):
        inside = inside or el.tag.split("}")[-1] == "DataTemplate"
        for attr_name, attr_value in el.attrib.items():
            if inside and "x:Reference" in attr_value:
                ok(False, f"{page}: x:Reference inside a DataTemplate "
                          f"({attr_name.split('}')[-1]}) - use "
                          f"Source={{RelativeSource AncestorType={{x:Type ContentPage}}}}")
        for child in el:
            walk_templates(child, inside)

    walk_templates(root)

# Border.Stroke is Brush-typed; assigning a Color token is the wrong type.
for page, text in PAGE_TEXT.items():
    for token in re.findall(r'Stroke="\{StaticResource ([A-Za-z0-9_]+)\}"', text):
        ok(token.endswith("Brush"), f"{page}: Stroke bound to Color token '{token}' (needs a Brush)")
for token in re.findall(r'Property="Stroke" Value="\{StaticResource ([A-Za-z0-9_]+)\}"', STYLES):
    ok(token.endswith("Brush"), f"Styles.xaml: Stroke bound to Color token '{token}' (needs a Brush)")

# A pill with no tone must still render its capsule: the control has to establish
# the neutral state itself rather than waiting for a Tone to be bound.
pill = open("Controls/StatusPill.cs", encoding="utf-8").read()
ok("ApplyTone();" in pill.split("private void ApplyTone()")[0],
   "Controls/StatusPill.cs: constructor does not apply the neutral tone (toneless pills render bare)")
ok('"BorderBrush"' in pill,
   "Controls/StatusPill.cs: default stroke must use the BorderBrush (Brush-typed) token")

# Converter/type agreement: a converter's contract is easy to violate silently
# (e.g. InvertedBoolConverter returns `value is not true`, so feeding it a string
# is always true). Each converter declares the type it accepts.
CONVERTER_INPUTS = {
    "InvertedBoolConverter": "bool",
    "StringHasContent": "string",
    "CountEmpty": "int",
    "CountPositive": "int",
}
EXPECTED_TYPES = {
    "bool": {"bool"},
    "string": {"string"},
    "int": {"int", "long"},
}
for page, text in PAGE_TEXT.items():
    root = ET.parse(page).getroot()
    page_type = (attr(root, "DataType") or "").split(":")[-1]

    def walk_converters(el, scope):
        nonlocal_scope = scope
        for raw in el.attrib.values():
            m = re.match(r"\{Binding\s+([A-Za-z0-9_.]+).*?Converter=\{StaticResource\s+(\w+)\}", raw)
            if not m:
                continue
            path, converter = m.group(1), m.group(2)
            expected = CONVERTER_INPUTS.get(converter)
            if not expected:
                continue
            actual = vb.member_type(scope, path)
            if actual is None:
                continue          # unknown type: unresolved rather than wrong
            ok(actual in EXPECTED_TYPES[expected],
               f"{page}: {converter} expects {expected} but '{path}' is {actual}")
        for child in el:
            walk_converters(child, attr(child, "DataType") or scope)

    walk_converters(root, page_type)

# Anything a user can tap must announce itself: a Border with a TapGestureRecognizer
# and a Command is an actionable control, so it needs a semantic description (a
# screen reader announces nothing otherwise - it is not a Button).
for page in PAGES:
    root = ET.parse(page).getroot()

    def walk_tappable(el):
        # MAUI writes the property element as "<Border.GestureRecognizers>", so the
        # local name ends with "GestureRecognizers" rather than equalling it.
        direct_gestures = [
            child for child in el
            if child.tag.split("}")[-1].endswith("GestureRecognizers")
        ]
        for holder in direct_gestures:
            for gesture in holder:
                if gesture.tag.split("}")[-1] != "TapGestureRecognizer":
                    continue
                if not gesture.get(f"{{{NS_MAUI}}}Command") and not gesture.get("Command"):
                    continue
                described = (el.get(f"{{{NS_MAUI}}}SemanticProperties.Description")
                             or el.get("SemanticProperties.Description")
                             or el.get("AutomationProperties.Name"))
                ok(bool(described),
                   f"{page}: tappable {el.tag.split('}')[-1]} with a Command has no "
                   f"SemanticProperties.Description (invisible to screen readers)")
        for child in el:
            walk_tappable(child)

    walk_tappable(root)

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
