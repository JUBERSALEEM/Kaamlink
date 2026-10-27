# KaamLink — Aurora Clay accessible soft-clay theme and theme engine

**Status:** design and source updates are implemented; the static contrast/redesign gates pass. Runtime appearance and theme switching have **not** been tested on a device in this environment.

See [`05-neumorphic-soft-ui-research.md`](05-neumorphic-soft-ui-research.md) for the research, rationale and references. This is an accessible soft-UI hybrid, not a shadow-only implementation or a claim that “neumorphism” is a platform standard.

## 1. Visual recipe

| Ingredient | Implementation | Location |
|---|---|---|
| Tonal surfaces | Aurora lavender/violet vertical gradients on page/card/soft-card, with contrast-checked top and bottom stops; chromatic midnight-violet dusk palette | `Resources/Styles/Colors.xaml`, `Resources/Themes/ClayDusk.xaml` |
| Soft depth | One restrained low-opacity cast shadow per elevated view | `Core/Presentation/Clay/ClayMaterial.cs` |
| Identifiable edges | Explicit 1dp card/field stroke and stronger native-button outlines; shadows are not the sole boundary | `Resources/Styles/Styles.xaml` |
| Rounded shapes | 24dp buttons, 28dp cards/banners, 22dp inset fields, pill icon tiles | `Resources/Styles/Styles.xaml` |
| Action/state | Saturated indigo/violet primary actions, cyan/mint accents, visible 0.97 scale pressed state, disabled opacity; outlined secondary buttons | `Resources/Styles/Styles.xaml` |
| Touch/accessibility | Native `Button` controls, text labels, semantic descriptions, 44dp+ theme/country controls and 48dp country rows | pages + styles |
| Theme repaint | Palette keys are `DynamicResource`; `ThemeService` swaps dictionaries and refreshes shadows | `Core/Presentation/Theming/` |

Page, card and soft-card surfaces use three-stop vertical gradients (`ClayPageBrush`, `ClayCardBrush`, `ClayCardSoftBrush`) in both palettes, so the lit top edge reads as clay without relying on a second shadow. Native `Button` controls use the **Brush-typed `Background`** property with `ClayPrimaryBrush` / `ClayCardBrush`: Android renders these gradients, while a `BackgroundColor` setter on a derived style would silently override an inherited gradient. The app does not claim a multi-shadow primitive, because MAUI `VisualElement` exposes one `Shadow` object per view.

## 2. Elevation

`ClayMaterial.Elevation` creates a fresh `Shadow` per element (a shadow instance is not shared through a style). Current casts are intentionally restrained but slightly wider than the first clay pass:

```text
Pill:   offset (3, 5),  radius 14, opacity 0.20
Card:   offset (5, 8),  radius 24, opacity 0.24
Raised: offset (7, 11), radius 30, opacity 0.28
```

The brush comes from `ClayShadowBrush`; the fallback is Aurora violet `#6E62A8`. `ThemeService` refreshes live-tree shadows after replacing the palette, because a `Shadow` is not itself a `DynamicResource`.

## 3. Theme engine

```text
"clay"      → Resources/Styles/Colors.xaml      (Aurora Clay, light)
"clay-dusk" → Resources/Themes/ClayDusk.xaml   (Aurora Dusk, rich dark)
Preference  → "kaamlink.theme"
```

The persisted IDs remain `clay` / `clay-dusk` for backward compatibility with saved preferences; the shipped palettes are the new Aurora designs. `App` resolves the saved theme before creating the Shell. `IThemeService.Toggle()` returns the theme id that was **actually applied**; the login and role-choice screens use native `Button`s and update their localized action label from that id. `DynamicResource` updates palette-bound colours in the live visual tree. Runtime switches instantiate the compiled `ClayPalette` / `ClayDuskPalette` dictionaries — a C# `ResourceDictionary` with only `Source` set does not load MAUI XAML. Theme dictionaries retain a parity of resource keys and types, enforced by `tools/verify-ui-redesign.py`.

## 4. Current palettes

### Aurora Clay (default, light)

| Role | Current value(s) |
|---|---|
| Page | `PageBackground #EBE9FF`; gradient stops `#F7F5FF` → `#DFE3FF` |
| Card | `CardSurface #EEEAFF`; gradient stops `#FBF9FF` → `#DED9F7` |
| Quiet surface | `CardSurfaceAlt #E2E7FF`; gradient stops `#F1F3FF` → `#D1D9F8` |
| Brand accent | `Primary #4B3FD6`; violet gradient `#8B7BFF` → `#3A2CBF`; mint `AccentTop #5DE3D4` |
| Primary fill (top/body/base) | `#6D5BEE / #4B3FD6 / #3323A6` |
| Accent fill (top/body/base) | `#0B6E7A / #0B6E7A / #084E59` |
| Ink (primary/secondary/muted) | `#201B45 / #3A3568 / #4F4A78` |
| Strong outline | `#6E62A8` |
| Shadow | `#8C84C4` |

### Aurora Dusk — rich midnight violet, not black

| Role | Current value(s) |
|---|---|
| Page | `PageBackground #140E2A`; gradient stops `#201542` → `#0D0A1E` |
| Card | `CardSurface #1C153A`; gradient stops `#2B2057` → `#150F2E` |
| Quiet surface | `CardSurfaceAlt #241A49`; gradient stops `#35265F` → `#1A1236` |
| Brand accent | `Primary #B9A9FF`; violet gradient `#D9CEFF` → `#8F7BFF`; mint `Accent #6FE7D2` |
| Primary fill (top/body/base) | `#7A59E8 / #6D4DE8 / #4E32B8` |
| Accent fill (top/body/base) | `#0E7A73 / #0B6E68 / #0A504B` |
| Ink (primary/secondary/muted) | `#F7F3FF / #D8CFF5 / #B6A9D8` |
| Strong outline | `#8F7BC8` |
| Shadow | `#050310` |

`Primary` is an accent for text/icons; `PrimaryFill*` is the filled-button ramp. In dusk they must differ: a text accent needs to be light, while white-on-fill needs a saturated darker fill. Both button ramps use `Button.Background` gradient brushes on Android, so the rich color is visible on the action surfaces themselves.

## 5. Static contrast result

Latest `python3 tools/verify-theme-contrast.py` result: **55 checks, 0 failures across both palettes**. Text pairs are checked at 4.5:1 and control outlines at 3:1 by the repository's palette gate. The checker is a static palette calculation, not a substitute for rendered/device review.

Useful latest sample ratios:

| Pair | Aurora Clay | Aurora Dusk |
|---|---:|---:|
| `TextOnPrimary` / `PrimaryFillTop` | 4.79:1 | 4.75:1 |
| `TextOnPrimary` / `AccentFillTop` | 5.96:1 | 5.19:1 |
| `BorderStrong` / `CardSurface` | 4.49:1 | 4.79:1 |
| `BorderStrong` / `PageBackground` | 4.44:1 | 5.18:1 |

## 6. Reproducible static checks

```bash
python3 tools/verify-theme-contrast.py
python3 tools/verify-ui-redesign.py
python3 tools/verify-global-phone-directory.py
python3 tools/verify-email-auth.py
```

All passed for the current workspace, along with `bash tools/verify-all.sh`. A MAUI build, Android launch, live theme switch, Arabic/RTL layout, larger-font review, and screenshot review remain unverified because the .NET/Android runtime toolchain is unavailable here.
