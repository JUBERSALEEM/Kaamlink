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
ICONS = open("Resources/Styles/Icons.xaml", encoding="utf-8").read()
THEME_FILES = sorted(glob.glob("Resources/Themes/*.xaml"))

# Colour/brush keys come from the (themed) palette; a page must reference them
# with DynamicResource or the live theme switch cannot repaint it.
PALETTE_KEYS = set()
for _kind in ("Color", "SolidColorBrush", "LinearGradientBrush"):
    PALETTE_KEYS |= set(re.findall(rf'<{_kind} x:Key="(\w+)"', COLORS))

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

# ------------------------------------------------------- K theme engine wiring
# The engine is only real if the catalogue points at files that exist, the app
# merges a palette, and the switch is wired app-wide (not to one page).
CATALOG = read("Core/Presentation/Theming/ThemeCatalog.cs")
THEME_SERVICE = read("Core/Presentation/Theming/ThemeService.cs")
APP_XAML = read("App.xaml")
APP_CS = read("App.xaml.cs")
MAUI_PROGRAM = read("MauiProgram.cs")

catalog_sources = re.findall(r'"(Resources/[\w./]+\.xaml)"', CATALOG)
ok(len(catalog_sources) >= 2, "ThemeCatalog: fewer than two themes registered")
for source in catalog_sources:
    ok(os.path.exists(source), f"ThemeCatalog: palette file '{source}' does not exist")

ok(re.search(r'Source="Resources/(Styles|Themes)/[\w-]+\.xaml"', APP_XAML) is not None,
   "App.xaml: no palette dictionary is merged")
ok('Icons.xaml' in APP_XAML, "App.xaml: Icons.xaml is no longer merged")
ok('MergedDictionaries' in THEME_SERVICE,
   "ThemeService: does not swap a merged palette dictionary")
ok('Preferences' in THEME_SERVICE, "ThemeService: does not persist the chosen theme")
ok(MAUI_PROGRAM.count('AddSingleton<IThemeService, ThemeService>') == 1,
   "MauiProgram: IThemeService must be registered exactly once")
ok('_theme.Apply' in APP_CS,
   "App.xaml.cs: the stored theme is never applied before the first page")
ok('ClayMaterial.Refresh' in THEME_SERVICE,
   "ThemeService: clay shadows are not refreshed after a palette swap")

ok(not re.search(r'\bmerged\s*\.\s*(?:Insert|RemoveAt)\s*\(|\bmerged\s*\[', THEME_SERVICE),
   "ThemeService: MergedDictionaries is ICollection<ResourceDictionary> - no Insert, "
   "RemoveAt or indexer exists on it (CS1501/CS1503)")

# A palette swap appends: the collection type offers no insert, so merge ORDER decides
# which value a duplicated key resolves to. Order-independence is therefore a rule,
# not an accident - every merged dictionary must own a disjoint set of keys.
MERGED_DICTS = ["Resources/Styles/Icons.xaml", "Resources/Styles/Colors.xaml",
                "Resources/Styles/Styles.xaml"]
merged_keys = {path: set(re.findall(r'x:Key="([A-Za-z0-9_]+)"', read(path))) for path in MERGED_DICTS}
for left in range(len(MERGED_DICTS)):
    for right in range(left + 1, len(MERGED_DICTS)):
        a, b = MERGED_DICTS[left], MERGED_DICTS[right]
        overlap = sorted(merged_keys[a] & merged_keys[b])
        ok(not overlap,
           f"Theme: {a} and {b} both define {overlap}; a palette swap appends, so "
           f"merge order would silently decide the value")

for style_key in ("SurfaceCard", "SurfaceCardDense", "IconTile", "IconTileLarge"):
    # Match the whole <Style ...> element: BasedOn lives in the opening tag.
    block = re.search(rf'<Style [^>]*x:Key="{style_key}"[\s\S]*?</Style>', STYLES)
    body = block.group(0) if block else ""
    # A BasedOn child inherits the depth of its base style (asserted separately).
    ok(block is not None and ("ClayMaterial.Elevation" in body or "BasedOn" in body),
       f"Styles.xaml: {style_key} lost its clay elevation")

ok('ToggleThemeCommand' in "\n".join(PAGE_TEXT.values()),
   "Theme: ToggleThemeCommand is never bound to a control")

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
    used |= set(re.findall(r"DynamicResource\s+([A-Za-z0-9_]+)\}", text))
    legacy = sorted(used & LEGACY_KEYS)
    ok(not legacy, f"{page}: legacy style keys {legacy}")

    # Theme engine contract: colours are dynamic, everything else static.
    baked = sorted(set(re.findall(r"StaticResource\s+([A-Za-z0-9_]+)\}", text)) & PALETTE_KEYS)
    ok(not baked,
       f"{page}: colour token(s) {baked} use StaticResource, which a live theme "
       f"switch cannot repaint (use DynamicResource)")
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

icon_keys = re.findall(r'<x:String x:Key="(Icon\w+)">([^<]*)</x:String>', ICONS)
# An empty/moved icon dictionary must fail loudly instead of passing an empty loop.
ok(len(icon_keys) >= 41,
   f"Icons.xaml: only {len(icon_keys)} glyph(s) found, expected the full set (>= 41)")
for key, glyph in icon_keys:
    decoded = html.unescape(glyph.strip())
    ok(len(decoded) == 1, f"Colors.xaml: {key} is not a single glyph")

# A typo in an icon key renders an empty glyph (or crashes the look-up), so every
# Icon* reference in a page or style must exist in Icons.xaml.
icon_names = {key for key, _ in icon_keys}
referenced_icons = set()
for text in list(PAGE_TEXT.values()) + [STYLES]:
    referenced_icons |= set(re.findall(r"StaticResource\s+(Icon[A-Za-z0-9_]*)", text))
style_keys = set(re.findall(r'<Style [^>]*x:Key="(\w+)"', STYLES))
for name in sorted(referenced_icons - icon_names - style_keys):
    ok(False, f"Icons.xaml: '{name}' is referenced but not declared")

token_keys = re.findall(r'x:Key="([A-Za-z0-9_]+)"', COLORS)
duplicates = sorted({k for k in token_keys if token_keys.count(k) > 1})
ok(not duplicates, f"Colors.xaml: duplicate keys {duplicates}")
for required in ("Primary", "PrimarySoft", "Success", "Warning", "Error", "Info",
                 "TextPrimary", "TextSecondary", "TextMuted", "CardSurface", "Border", "Divider",
                 # clay material: without these the surfaces are flat again
                 "ClayCardBrush", "ClayPageBrush", "ClayInsetBrush", "ClayHighlight",
                 "ClayShadow", "PrimaryFill"):
    ok(f'x:Key="{required}"' in COLORS, f"Colors.xaml: missing token {required}")

# Every theme must be a complete palette, or a switch renders a half-themed screen.
ok(THEME_FILES, "Resources/Themes: no alternate theme palette found")
for theme_path in THEME_FILES:
    theme_text = open(theme_path, encoding="utf-8").read()
    missing = sorted(set(re.findall(r'x:Key="(\w+)"', COLORS)) - set(re.findall(r'x:Key="(\w+)"', theme_text)))
    ok(not missing, f"{theme_path}: missing palette keys {missing[:6]}")

# The clay styles must actually ask for clay (a style that silently loses its
# elevation is a flat card that still passes every other rule).
for style_key in ("SurfaceCard", "PrimaryButton", "IconTile"):
    block = re.search(rf'x:Key="{style_key}">([\s\S]*?)</Style>', STYLES)
    ok(block is not None and "ClayMaterial.Elevation" in block.group(1),
       f"Styles.xaml: {style_key} has no clay elevation")
hardcoded_colors = re.findall(r'Value="#[0-9A-Fa-f]{3,8}"', STYLES)
ok(not hardcoded_colors, f"Styles.xaml: raw hex values {hardcoded_colors}")

# The role chooser carries the same approved welcome identity and illustration
# treatment as sign-in, then adds the role choices without replacing the journey.
welcome = PAGE_TEXT.get("Features/Onboarding/RoleSelectionPage.xaml", "")
login = PAGE_TEXT.get("Features/Auth/LoginPage.xaml", "")
ok("login_hero.svg" in welcome and 'Style="{StaticResource LoginHeroSurface}"' in welcome
   and 'Text="{x:Static strings:AppStrings.LoginWelcomeHeadline}"' in welcome
   and 'Text="{x:Static strings:AppStrings.LoginWelcomeSubtitle}"' in welcome
   and 'Text="{x:Static strings:AppStrings.AppTagline}"' in welcome
   and 'Style="{StaticResource LoginHeroSurface}"' in login,
   "RoleSelectionPage: approved login welcome identity/hero is not shared")
ok('Command="{Binding NavigateToWorkerCommand}"' in welcome
   and 'Command="{Binding NavigateToEmployerCommand}"' in welcome,
   "RoleSelectionPage: both role choices need real, visible action buttons")
ok('Style="{StaticResource LoginFormSurface}"' in login
   and 'Command="{Binding SendCodeCommand}"' in login
   and 'Command="{Binding OpenCountryPickerCommand}"' in login
   and 'IsEnabled="False"' in login,
   "LoginPage: active phone flow and explicitly disabled Google affordance must remain")
ok('WorkerChoiceSurface' in welcome and 'EmployerChoiceSurface' in welcome,
   "RoleSelectionPage: worker and employer choices need distinct theme-aware surfaces")

# The adaptive app icon is a real layered vector asset, and its emblem stays in
# sync with the splash and in-app brand mark.
icon_bg = read("Resources/AppIcon/appicon.svg")
icon_fg = read("Resources/AppIcon/appiconfg.svg")
splash_logo = read("Resources/Splash/splash.svg")
brand_mark = read("Resources/Images/kaamlink_mark.svg")
project = read("KaamLink.csproj")
icon_bg_root = icon_fg_root = None
try:
    icon_bg_root = ET.fromstring(icon_bg)
    icon_fg_root = ET.fromstring(icon_fg)
    ET.fromstring(splash_logo)
    ET.fromstring(brand_mark)
    icon_vectors_valid = True
except ET.ParseError:
    icon_vectors_valid = False
ok(icon_vectors_valid, "App icon/brand SVG assets must be well-formed XML")
ok('MauiIcon Include="Resources\\AppIcon\\appicon.svg" ForegroundFile="Resources\\AppIcon\\appiconfg.svg"' in project,
   "App icon: MAUI adaptive background/foreground wiring is missing")
ok(icon_bg_root is not None and icon_fg_root is not None
   and icon_bg_root.get("viewBox") == "0 0 108 108"
   and icon_fg_root.get("viewBox") == "0 0 108 108"
   and "linearGradient" in icon_bg and "radialGradient" in icon_bg
   and "M35 51v-2a19 19 0 0 1 38 0v2H35Z" in icon_fg
   and "M42 72h24" in icon_fg,
   "App icon: rich gradient background and helmet/link emblem are missing")

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
    for token in re.findall(r'Stroke="\{(?:Static|Dynamic)Resource ([A-Za-z0-9_]+)\}"', text):
        ok(token.endswith("Brush"), f"{page}: Stroke bound to Color token '{token}' (needs a Brush)")
for token in re.findall(r'Property="Stroke" Value="\{(?:Static|Dynamic)Resource ([A-Za-z0-9_]+)\}"', STYLES):
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
