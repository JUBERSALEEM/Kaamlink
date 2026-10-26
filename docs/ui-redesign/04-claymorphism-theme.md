# KaamLink — accessible soft-clay theme and theme engine

**Status:** design and source updates are implemented; the static contrast/redesign gates pass. Runtime appearance and theme switching have **not** been tested on a device in this environment.

See [`05-neumorphic-soft-ui-research.md`](05-neumorphic-soft-ui-research.md) for the research, rationale and references. This is an accessible soft-UI hybrid, not a shadow-only implementation or a claim that “neumorphism” is a platform standard.

## 1. Visual recipe

| Ingredient | Implementation | Location |
|---|---|---|
| Tonal surfaces | Cool grey-blue page, near-tone cards; equivalent charcoal dusk palette | `Resources/Styles/Colors.xaml`, `Resources/Themes/ClayDusk.xaml` |
| Soft depth | One restrained low-opacity cast shadow per elevated view | `Core/Presentation/Clay/ClayMaterial.cs` |
| Identifiable edges | Explicit 1dp card/field stroke and stronger native-button outlines; shadows are not the sole boundary | `Resources/Styles/Styles.xaml` |
| Action/state | Saturated primary actions, visible 0.97 scale pressed state, disabled opacity; outlined secondary buttons | `Resources/Styles/Styles.xaml` |
| Touch/accessibility | Native `Button` controls, text labels, semantic descriptions, 44dp+ theme/country controls and 48dp country rows | pages + styles |
| Theme repaint | Palette keys are `DynamicResource`; `ThemeService` swaps dictionaries and refreshes shadows | `Core/Presentation/Theming/` |

The current page/card surfaces are solid tonal surfaces. Gradient fills are retained for brand actions; the app does not claim a multi-shadow primitive, because MAUI `VisualElement` exposes one `Shadow` object per view.

## 2. Elevation

`ClayMaterial.Elevation` creates a fresh `Shadow` per element (a shadow instance is not shared through a style). Current casts are intentionally restrained:

```text
Pill:   offset (3, 4),  radius 12, opacity 0.18
Card:   offset (5, 7),  radius 20, opacity 0.22
Raised: offset (7, 10), radius 26, opacity 0.26
```

The brush comes from `ClayShadowBrush`. `ThemeService` refreshes live-tree shadows after replacing the palette, because a `Shadow` is not itself a `DynamicResource`.

## 3. Theme engine

```text
"clay"      → Resources/Styles/Colors.xaml
"clay-dusk" → Resources/Themes/ClayDusk.xaml
Preference  → "kaamlink.theme"
```

`App` resolves the saved theme before creating the Shell. `IThemeService.Toggle()` returns the next theme id; the login and role-choice screens use native `Button`s and update their localized action label from that id. `DynamicResource` updates palette-bound colours in the live visual tree. Theme dictionaries retain a parity of resource keys and types, enforced by `tools/verify-ui-redesign.py`.

## 4. Current palettes

### Clay (default)

| Role | Current value(s) |
|---|---|
| Page | `PageBackground #E9EDF4` |
| Card | `CardSurface #E9EDF4`; `CardSurfaceTop #F6F8FB` retained for token compatibility |
| Quiet surface | `CardSurfaceAlt #E0E6EE` |
| Primary fill (top/body/base) | `#2E74C6 / #0B57A4 / #084A8C` |
| Accent fill (top/body/base) | `#1B8078 / #0B6660 / #095651` |
| Ink (primary/secondary/muted) | `#172638 / #34475C / #50637A` |
| Strong outline | `#6E8198` |
| Shadow | `#9AA9BA` |

### Clay Dusk

| Role | Current value(s) |
|---|---|
| Page | `PageBackground #202731` |
| Card | `CardSurface #242C36` |
| Quiet surface | `CardSurfaceAlt #29333E` |
| Primary fill (top/body/base) | `#3A72BC / #2E62A8 / #234C86` |
| Accent fill (top/body/base) | `#17847A / #106B63 / #0B544E` |
| Ink (primary/secondary/muted) | `#F1F4F7 / #D6DEE7 / #B8C4D0` |
| Strong outline | `#9AAABD` |
| Shadow | `#10151B` |

`Primary` is an accent for text/icons; `PrimaryFill*` is the filled-button ramp. In dusk they must differ: a text accent needs to be light, while white-on-fill needs a saturated darker fill.

## 5. Static contrast result

Latest `python3 tools/verify-theme-contrast.py` result: **55 checks, 0 failures across both palettes**. Text pairs are checked at 4.5:1 and control outlines at 3:1 by the repository's palette gate. The checker is a static palette calculation, not a substitute for rendered/device review.

Useful latest sample ratios:

| Pair | Clay | Dusk |
|---|---:|---:|
| `TextOnPrimary` / `PrimaryFillTop` | 4.74:1 | 4.87:1 |
| `TextOnPrimary` / `AccentFillTop` | 4.77:1 | 4.55:1 |
| `BorderStrong` / `CardSurface` | 3.40:1 | 5.95:1 |
| `BorderStrong` / `PageBackground` | 3.40:1 | 6.35:1 |

## 6. Reproducible static checks

```bash
python3 tools/verify-theme-contrast.py
python3 tools/verify-ui-redesign.py
python3 tools/verify-global-phone-directory.py
```

All three passed for the current workspace. A MAUI build, Android launch, live theme switch, Arabic/RTL layout, larger-font review, and screenshot review remain unverified because the .NET/Android runtime toolchain is unavailable here.
