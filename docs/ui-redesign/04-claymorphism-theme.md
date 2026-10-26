# KaamLink — Claymorphism theme and theme engine

Status: **implemented, statically verified, not yet seen on a device.** Every claim below is
checked by a gate listed in §7; runtime appearance is still unverified (§8).

Companion documents: `02-design-system-specification.md` (typography, spacing, control
inventory — still current) and `03-implementation-and-verification-report.md` (the redesign
and its verification). This document covers the **visual material** and the **engine**.

---

## 1. What "clay" means here, concretely

Claymorphism is soft, matte, moulded plastic. In this app it is built from four ingredients,
not from one drop shadow:

| Ingredient | Implementation | Where |
|---|---|---|
| **Lit gradient** — surfaces are lighter at the top, as if lit from above | `LinearGradientBrush` tokens (`ClayCardBrush`, `ClayPageBrush`, `ClayInsetBrush`, …) | `Resources/Styles/Colors.xaml` |
| **Soft cast shadow** — wide, low opacity, desaturated blue | `Shadow` with `Radius` 10–34 and `Opacity` 0.22–0.34 | `Core/Presentation/Clay/ClayMaterial.cs` |
| **Light edge** — a 1–1.5dp highlight stroke on the moulded surfaces | `ClayHighlight` stroke on cards, tiles, pills, fields | `Resources/Styles/Styles.xaml` |
| **Generous radii** — 18–26dp on surfaces, 999 on pills | `RoundRectangle CornerRadius` / `Button.CornerRadius` | styles + pages |

Deliberate restraint (unchanged from the redesign rules): no oversized headings, no neon, no
glassy blur, no per-icon floating shadows, no gradients on text. Clay is a *material*, the
layout stays quiet and premium.

**Press feedback.** Buttons move through `VisualStateManager`: `Pressed` → `Scale 0.97` +
`Opacity 0.94`, `Disabled` → `Opacity 0.45`. That reads as soft material being pushed in.

---

## 2. Why an attached property instead of a shadow in the style

A `Shadow` object belongs to one element. Putting one in a `Style` setter would share a single
instance across every card, tile and button that uses the style — the wrong ownership model,
and the kind of thing that shows up as "shadows randomly missing on some screens".

`ClayMaterial.Elevation` is an attached property; its change handler builds a **fresh**
`Shadow` per element:

```xml
<Setter Property="clay:ClayMaterial.Elevation" Value="Card" />   <!-- in the style -->
```

```csharp
// Core/Presentation/Clay/ClayMaterial.cs
ClayElevation.Pill   → offset 4,  radius 10, opacity 0.22   // pills, chips, small buttons
ClayElevation.Card   → offset 10, radius 26, opacity 0.30   // cards, panels
ClayElevation.Raised → offset 14, radius 34, opacity 0.34   // primary buttons, bottom docks
```

Colours come from the active palette (`ClayShadowBrush`), so the shadow follows the theme.

---

## 3. The engine

```
ThemeCatalog.All                    ordered list of themes: id + palette dictionary path
  "clay"       → Resources/Styles/Colors.xaml
  "clay-dusk"  → Resources/Themes/ClayDusk.xaml

IThemeService / ThemeService        applies a palette, persists the choice
  Apply(themeId)                    swaps the merged palette dictionary, refreshes shadows
  Toggle()                          next theme in the catalogue
  CurrentThemeId, ThemeChanged      state + notification
  Preferences "kaamlink.theme"      survives restarts
```

**Startup.** `App` receives `IThemeService` through DI and calls `_theme.Apply(CurrentThemeId)`
in `CreateWindow`, before the shell is built, so a dusk-theme user never sees a light flash.

**Switching.** `ThemeService.Apply` removes every merged dictionary whose `Source` is a known
palette and inserts the replacement at the same position (removing *all* of them, not just the
active one, so a path that stops matching cannot leave a duplicate merged). Remove + insert (rather than assigning into
the slot) is what makes MAUI raise the resource change that repaints every `DynamicResource`
consumer. On the next dispatcher turn it walks the live visual tree and re-applies clay
shadows (`ClayMaterial.Refresh`), because a `Shadow` object is not a `DynamicResource` — and a
turn of delay guarantees the replacement dictionary has loaded before a shadow reads
`ClayShadowBrush` from it.

**Why colours are `DynamicResource` everywhere.** A theme switch can only repaint values that
were bound dynamically. Since the redesign, every colour/brush token in every page and style
is `{DynamicResource …}`; `{StaticResource …}` is reserved for styles, icons and converters.
`tools/verify-ui-redesign.py` fails if a page bakes a palette token statically — that was a
real gap in the first draft of this work, now a rule with a negative test.

**A theme is a palette, never a layout.** `Styles.xaml` and all 15 pages are shared by both
themes; the only per-theme file is the dictionary. Key parity and resource-kind order are
enforced, so a new theme cannot silently drop a token.

### Adding a theme

1. Copy `Resources/Themes/ClayDusk.xaml` to `Resources/Themes/<Name>.xaml`.
2. Replace every colour value. Keep **every key** — the gate compares key sets and resource
   kinds, and `tools/verify-theme-contrast.py` measures the text pairs.
3. Add one line to `ThemeCatalog.All`.
4. Run `python3 tools/verify-theme-contrast.py` and `python3 tools/verify-ui-redesign.py`.

No page, style or view-model change is needed.

---

## 4. Palettes

### 4.1 Clay (default, light)

| Role | Token | Value |
|---|---|---|
| Page | `PageBackgroundTop/Bottom` | `#F5F3FD` → `#E3E8F8` |
| Card | `CardSurfaceTop/Surface/Bottom` | `#FFFFFF` → `#F9FAFE` → `#E9EDF9` |
| Quiet surface | `CardSurfaceAltTop/Alt/Bottom` | `#EFF2FB` → `#E5EAF7` → `#DCE2F2` |
| Brand fill | `PrimaryFillTop/Fill/Bottom` | `#2E74C6` → `#0B57A4` → `#084A8C` |
| Brand accent (text/icon) | `Primary` | `#0B57A4` |
| Accent fill | `AccentFillTop/Fill/Bottom` | `#1B8078` → `#0B6660` → `#095651` |
| Ink | `TextPrimary` / `TextSecondary` / `TextMuted` | `#171F36` / `#3A4460` / `#5C6784` |
| Edge | `ClayHighlight` / `ClayEdgeLow` | `#FFFFFF` / `#C9D1E8` |
| Shadow | `ClayShadow` | `#6E7BA8` |

### 4.2 Clay Dusk (alternate, dark)

Dark clay inverts the depth cue: the top of a surface is only *slightly* lighter, the base is
much darker, and the cast shadow is black at a higher opacity.

| Role | Token | Value |
|---|---|---|
| Page | `PageBackgroundTop/Bottom` | `#252C49` → `#161B2C` |
| Card | `CardSurfaceTop/Surface/Bottom` | `#3A4364` → `#2C3450` → `#242A44` |
| Brand accent (text/icon) | `Primary` | `#9CC2F0` |
| Brand fill (buttons) | `PrimaryFillTop/Fill/Bottom` | `#3A72BC` → `#2E62A8` → `#234C86` |
| Ink | `TextPrimary` / `TextSecondary` / `TextMuted` | `#F3F5FF` / `#C9D0E8` / `#A5AECB` |
| Shadow | `ClayShadow` | `#000000` |

**Why `Primary` and `PrimaryFill` are separate tokens.** On a dark theme, the accent used for
*text and icons* must be light (to sit on dark clay), while a *filled button* must stay
saturated enough for white text. One token cannot satisfy both, so the palette exposes
`Primary` (accent) and `PrimaryFill*` (fill). In the light theme they hold the same blue.

---

## 5. Contrast

`tools/verify-theme-contrast.py` measures 26 text/UI pairs per palette against WCAG AA
(4.5:1 text, 3:1 non-text) and fails the build otherwise.

| Theme | Pairs | Failing | Lowest measured |
|---|---|---|---|
| Clay | 26 | 0 | 3.11:1 (`BorderStrong` on `PageBackground`, needs 3.0) |
| Clay Dusk | 26 | 0 | 4.41:1 (`BorderStrong` on `CardSurface`, needs 3.0) |

Tight pairs worth knowing when editing the palettes:

* `BorderStrong` on `PageBackground`: **3.11:1** (light) against a 3.0 floor — the
  tightest value in either theme; an input outline is the first thing a palette edit breaks.
* `TextOnPrimary` on the lit top of a primary button: **4.74:1** (light) / **4.87:1** (dusk).
* `TextMuted` on `CardSurfaceAlt`: **4.68:1** (light) — little headroom for a lighter tint.
* `TextOnPrimary` on `AccentFillTop`: **4.55:1** — the lowest text value in the dusk theme.

Darkening a soft tint or lightening a muted ink will break these first.

---

## 6. What changed for the theme

```
Resources/Styles/Colors.xaml      palette (clay light) + clay brushes; icons removed
Resources/Styles/Icons.xaml       NEW — 43 Lucide glyphs, theme-independent
Resources/Styles/Styles.xaml      clay recipes; colours via DynamicResource; clay elevation
Resources/Themes/ClayDusk.xaml    NEW — alternate dark palette, same key set
Core/Presentation/Clay/           NEW — ClayMaterial attached property + ClayElevation
Core/Presentation/Theming/        NEW — ThemeCatalog, IThemeService, ThemeService
App.xaml / App.xaml.cs            merge Icons; apply the stored theme before the shell
MauiProgram.cs                    register IThemeService (singleton, once)
Features/Onboarding/*             theme toggle (pill) on the entry screen + its command
Controls/{NoticeBanner,StatePanel}  theme-reactive colours, clay radii/elevation
Features/**/*.xaml                46 colour refs → DynamicResource; panel radii on the clay scale
```

`Resources/Strings/AppStrings.*` gained 3 keys (315 EN/AR/Designer, parity checked):
`ThemeSwitchToDusk`, `ThemeSwitchToLight`, `ErrorOfflineTitle`.

---

## 7. How this is verified

| Gate | What it proves |
|---|---|
| `verify-theme-contrast.py` | key parity + resource kinds across palettes, 26 contrast pairs each (55 checks) |
| `verify-ui-redesign.py` §K | catalogue files exist, palette merged, icons merged, service registered once, theme applied at startup, shadows refreshed on switch, card/tile styles keep clay elevation, toggle command bound |
| `verify-ui-redesign.py` §B | no page bakes a palette token with `StaticResource`; no raw hex; no legacy style keys |
| `verify-ui-redesign.py` (icons) | every `Icon*` reference exists; the icon file cannot be empty |
| `verify-bindings.py` | the new theme bindings (`ThemeToggleLabel`, `IsDusk`, `ToggleThemeCommand`) resolve |
| `verify-compile-surface.py` | the new types are reachable from every file that uses them |
| `verify-gates-selftest.py` | 30 injected defects, including “colour baked with StaticResource”, “icon key missing”, “clay elevation dropped”, “theme palette missing a token” |

---

## 8. What is still unverified

* **Appearance on a device.** No emulator or device in this environment: gradients, shadow
  softness and the pressed state have never been rendered. Shadow rendering on Android depends
  on the element having a background — every clay surface does, but this needs eyes on it.
* **Live switch timing.** Whether MAUI repaints every `DynamicResource` consumer within the
  same frame after the dictionary swap is not measurable here; the mechanism (remove + insert)
  is the documented one, and the shadow refresh is explicit.
* **Dark theme legibility of the brand gradient** — contrast is measured, perceived softness is
  not.
* Anything the real compiler or XamlC rejects: `verify-compile-surface.py` is a proxy, not a
  compiler.
