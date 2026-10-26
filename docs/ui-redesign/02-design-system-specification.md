# KaamLink Design System — v1.0 "Marine Blue + Trust Teal"


> **v2 update — clay material.** The typography, spacing, control inventory and
> accessibility rules in this document still apply unchanged. The *visual material*
> (surfaces, elevation, palette, and how a theme is applied at runtime) is now
> claymorphism and is specified in [`04-claymorphism-theme.md`](04-claymorphism-theme.md).
> Where this document names a flat card or a 12–14dp radius, the clay spec supersedes it.

**Applies to:** the whole Android MAUI app (`net10.0-android`, min SDK 21)
**Baseline device:** 360 × 780 dp, 2× density; must also work at 320 dp and 412 dp
**Direction chosen after the audit:** the old palette was a generic indigo/violet SaaS set
(`#4F46E5` + `#7C3AED` + amber) while the Android chrome was teal (`#0F766E`) and the
splash/icon were indigo — three conflicting stories. KaamLink's actual promise is
*trustworthy verification of practical skill*, so v1.0 uses **one confident marine blue**
for action and brand, and a **restrained teal** reserved for verification/trust moments
(it also reconciles the platform teal). Everything else is a neutral that stays out of
the way.

All values below are implemented as tokens in `Resources/Styles/Colors.xaml` and styles
in `Resources/Styles/Styles.xaml`. **Mockups use the identical hex values** — the mockup
kit (`docs/ui-redesign/mockups/mockup_kit.py`) is byte-checked against the XAML by
`tools/verify-ui-redesign.py`.

---

## 1. Color tokens

> **Historical spec.** The shipped palette was upgraded to rich electric-blue /
> mint clay gradients and a midnight-navy dark theme. The authoritative current
> values are in `04-claymorphism-theme.md` and the two `ResourceDictionary` files;
> the table below documents the original flat-pass baseline.

### 1.1 Brand

| Token | Hex | Use |
|---|---|---|
| `Primary` | `#0B57A4` | primary buttons, links, selected chips, focus ring, active dots |
| `PrimaryDark` | `#084A8C` | pressed state of primary surfaces |
| `PrimarySoft` | `#E9F1F9` | selected rows, subtle buttons, info surfaces |
| `Accent` | `#0B6660` | verification & assessment trust accents (icons, marks) |
| `AccentSoft` | `#E4F2F0` | trust surfaces (verification explainer, "why it matters") |

### 1.2 Semantic status

| Token | Hex | Soft token | Soft hex | Meaning |
|---|---|---|---|---|
| `Success` | `#11652F` | `SuccessSoft` | `#E7F4EC` | verified company, active vacancy, shortlisted, hired, passed, assessed |
| `Warning` | `#9A5B00` | `WarningSoft` | `#FCF3E3` | pending review, paused vacancy, resend countdown |
| `Error` | `#B3261E` | `ErrorSoft` | `#FBECEB` | rejected, closed, destructive actions, validation errors |
| `Info` | `#0B57A4` | `InfoSoft` | `#E9F1F9` | neutral informational (aliases Primary) |

### 1.3 Neutrals & surfaces

| Token | Hex | Use |
|---|---|---|
| `TextPrimary` | `#111A24` | headings, titles, values |
| `TextSecondary` | `#3C4753` | body copy |
| `TextMuted` | `#66717F` | captions, metadata, placeholders |
| `TextOnPrimary` | `#FFFFFF` | text on Primary/Success/Error fills |
| `TextDisabled` | `#9AA5B1` | disabled labels (never the only signal for state) |
| `PageBackground` | `#F5F7F9` | app canvas |
| `CardSurface` | `#FFFFFF` | cards, fields, sheets |
| `CardSurfaceAlt` | `#FAFBFC` | neutral pill/row background |
| `Border` | `#DEE3E9` | card outlines, dividers between cards |
| `BorderStrong` | `#7E8B99` | **input field outlines** (needs 3:1 non-text contrast) |
| `Divider` | `#EDF0F4` | in-card separators, progress tracks |
| `White` / `Black` | `#FFFFFF` / `#000000` | kept for compatibility; `Black` only in shadow/scrim definitions |

### 1.4 Contrast (measured, not assumed)

| Pair | Ratio | WCAG |
|---|---|---|
| TextPrimary on CardSurface | 17.54 | AAA |
| TextPrimary on PageBackground | 16.34 | AAA |
| TextSecondary on CardSurface | 9.47 | AAA |
| TextMuted on CardSurface | 4.96 | AA |
| TextMuted on CardSurfaceAlt | 4.79 | AA |
| Primary (text/links) on CardSurface | 7.21 | AAA |
| TextOnPrimary on Primary | 7.21 | AAA |
| Primary on PrimarySoft | 6.32 | AA |
| Accent on AccentSoft | 5.92 | AA |
| Success on SuccessSoft | 6.34 | AA |
| Warning on WarningSoft | 4.93 | AA |
| Error on ErrorSoft | 5.70 | AA |
| Error on CardSurface | 6.54 | AA |
| BorderStrong vs CardSurface (non-text) | 3.48 | AA (1.4.11) |
| Primary vs PageBackground (focus ring) | 6.71 | AA |

**Rule:** status is never conveyed by color alone — every pill/banner carries a text label
and, where it matters, an icon.

---

## 2. Typography

Typeface: **Open Sans** (already in the project: `OpenSansRegular`, `OpenSansSemibold`).
Android's system Arabic face renders Arabic automatically; the app never switches family
per language, and Arabic inherits the same sizes.

| Style key | Size | Line height | Weight | Color | Used for |
|---|---|---|---|---|---|
| `DisplayText` | 25 | 31 | Semibold | TextPrimary | welcome headline, assessment score |
| `TitleText` | 21 | 27 | Semibold | TextPrimary | screen titles, vacancy title, OTP/login headline |
| `SectionText` | 17 | 23 | Semibold | TextPrimary | section headings, card group titles |
| `CardTitleText` | 15.5 | 21 | Semibold | TextPrimary | card titles, list-row titles, question text |
| `BodyText` | 14.5 | 20.5 | Regular | TextSecondary | body copy, descriptions |
| `BodyStrongText` | 14.5 | 20.5 | Semibold | TextPrimary | values, names, key data |
| `SmallText` | 13.5 | 19 | Regular | TextSecondary | secondary lines, meta, cover notes |
| `CaptionText` | 12.5 | 17 | Regular | TextMuted | timestamps, helper text, footnotes |
| `LabelText` | 13 | 17.5 | Semibold | TextPrimary | form field labels |
| `OverlineText` | 12 | 15.5 | Semibold | TextSecondary | section eyebrows / section headers |

Rules:

* No text smaller than **12sp**; body is **14.5sp** (≥14sp requirement).
* Section eyebrows are **not** uppercased and carry **no letter spacing**: both transform
  Latin-only and degrade (and in Arabic, break) the script. `OverlineText` is 12sp semibold.
  This is a post-mockup correction made during implementation — the mockups were drawn
  before it and show the earlier eyebrow treatment.
* Ordinary operational screens never use a >21sp heading; `DisplayText` is limited to the
  welcome headline and the assessment score.
* Captions are never used for errors — errors use `SmallText` in `Error` on `ErrorSoft`.
* Arabic renders at the same sizes; the platform face is slightly taller, so all
  text containers are allowed to grow (no fixed heights on text rows).

---

## 3. Spacing, radii, elevation

| Token | dp | Use |
|---|---|---|
| `Space2` | 2 | hairline nudges |
| `Space4` | 4 | icon-to-label gaps, tight stacks |
| `Space6` | 6 | pill gaps |
| `Space8` | 8 | inside-card row gaps |
| `Space10` | 10 | between related controls |
| `Space12` | 12 | card-to-card rhythm |
| `Space16` | 16 | **screen margin**, card padding, section gaps |
| `Space20` | 20 | large section separation |
| `Space24` | 24 | dock padding, hero blocks |

* Screen margin is **16dp** on every screen (the old app used 20–24dp inconsistently).
* Card padding is **14dp** (dense lists) or **16dp** (forms), never both on one screen.
* Vertical rhythm: 12dp between cards, 16dp between sections, 8dp between rows.

Radii: `RadiusChip` 11 (buttons/fields), `RadiusCard` 14, `RadiusTile` 13, `RadiusPill` 999.
No 20dp+ radii; nothing fully circular except avatars and icon dots.

Elevation: **flat by default.** Cards use `Border` 1dp outlines, no shadow. A single
`Shadow` is allowed on the bottom action dock (`Opacity 0.06`, `Radius 12`, `Offset 0,-2`)
because it must read as an overlay. This removes the per-card shadow of the old design
(measurable over-draw cost on the low-end devices this product targets).

---

## 4. Iconography

* **Lucide** icon font (ISC licence) at `Resources/Fonts/lucide.ttf`, family name `Lucide`.
  Committed with its licence file; each glyph is a single unicode codepoint verified
  against the font's cmap (`tools/verify-ui-redesign.py` re-verifies every codepoint used
  in XAML).
* Sizes: **16** inline with 13–14.5sp text, **18** in compact rows, **22** in tiles,
  **17–19** in app bars. Colour comes from the semantic tokens.
* **Emoji are forbidden** in production UI (the old role-selection screen used 👷/🏭).
* Every icon that carries meaning also has text next to it; icon-only affordances
  (remove-skill, app-bar actions) always have a `SemanticProperties.Description`.

Icon set actually used by the app:

| Purpose | Glyph | Codepoint |
|---|---|---|
| brand mark | hard-hat | U+E0EE |
| employer / company | building-2 | U+E290 |
| vacancy / work | briefcase-business | U+E5D9 |
| location | map-pin | U+E111 |
| salary | banknote | U+E052 |
| openings | users | U+E1A4 |
| applications (worker) | clipboard-list | U+E086 |
| applicants (employer) | users | U+E1A4 |
| verified | badge-check | U+E241 |
| assessment | shield-check | U+E1FF |
| passport star | star | U+E176 |
| clock / availability | clock | U+E087 |
| back | arrow-left (mirrored in RTL by the platform) | U+E048 |
| forward / disclosure | chevron-right | U+E06F |
| add | plus | U+E13D |
| close / remove | x | U+E1B2 |
| pause / play | pause / play | U+E12E / U+E13C |
| success | circle-check | U+E226 |
| error | circle-alert | U+E077 |
| warning | triangle-alert | U+E193 |
| info | info | U+E0F9 |
| privacy | lock | U+E10B |
| phone | phone | U+E133 |
| sign out | log-out | U+E10E |
| switch role | user-round | U+E46C |
| language | globe | U+E0E8 |
| edit | pencil | U+E1F9 |
| viewed | eye | U+E0BA |

---

## 5. Components

### 5.1 Buttons

| Variant | Height | Fill | Text | Border | Use |
|---|---|---|---|---|---|
| `PrimaryButton` | 48 | Primary | TextOnPrimary | — | the one main action of a screen/region |
| `SecondaryButton` | 48 | CardSurface | Primary | Border 1 | alternate action (back, secondary choice) |
| `SubtleButton` | 44 | PrimarySoft | Primary | — | dense in-card actions (start test, add skill) |
| `DangerButton` | 44 | ErrorSoft | Error | — | destructive in-card action (withdraw, reject) |
| `DangerSolidButton` | 48 | Error | TextOnPrimary | — | final confirmation of destruction |
| `TextButton` | 40 | transparent | Primary | — | tertiary navigation (switch role, links) |
| `DisabledButton` | 48 | `#EEF1F4` | TextDisabled | — | disabled; always paired with an on-screen reason |

Rules: one filled primary per region; never two primaries side by side; primary on the
end side in RTL automatically via layout direction; `HeightRequest` ≥ 44 for any tappable
button; icon (if any) precedes the label with 8dp.

### 5.2 Form fields

`FieldLabel` (12.5sp semibold) → 6dp → field box (46dp, `CardSurface`, `BorderStrong`
outline, radius 11, 14dp horizontal padding) → 6dp → `FieldHint` (12sp, TextMuted).
Error: outline `Error` 1.5dp + message in `Error` on `ErrorSoft` below the field.
Multi-line (Editor) boxes are 46 + 18 × lines tall, minimum 84dp.

### 5.3 Status pills

Height 22, radius 999, 9dp horizontal padding, 11.5sp semibold, 1dp same-hue outline,
soft background, optional 12dp leading icon. Full mapping:

| Domain | Pill | Tone |
|---|---|---|
| Company verification | Pending review / Verified company / Rejected | warning / success / error |
| Vacancy | Draft / Active / Paused / Closed | muted / success / warning / error |
| Application | Applied / Viewed / Shortlisted / Rejected / Hired / Withdrawn | primary / neutral / success / error / success / muted |
| Skill | Self-declared / Assessed | neutral / success |

The raw enum string still drives the tone through the existing converters, so business
meaning is untouched.

### 5.4 Cards & list rows

Card: `CardSurface`, `Border` 1, radius 14, padding 14–16, **no shadow**.
List row (inside a card): 48dp minimum tall, icon 16 + label, `Divider` between rows,
no nested cards. Vacancy card anatomy: initial avatar 34 → title (15.5 semibold) →
company · openings (12 muted) → location row → salary row (semibold) → verified pill →
skill chips. Applicant card: avatar 40 + name + meta + status pill → optional cover-note
quote (2dp left rule) → 2 × 2 decision grid.

### 5.5 Chips (filters)

Height 32, radius 16, 6dp gaps, horizontal scroll; selected = Primary fill + white text;
unselected = CardSurface + Border + TextSecondary. Chips replace the old `Picker` filter
**but keep the same `SelectedEmirate` binding** (an `EnumOption` list drives them).

### 5.6 Feedback states

| State | Pattern |
|---|---|
| Loading | centred `ActivityIndicator` (Primary) + one-line caption; the container reserves its height so content doesn't jump; page still scrollable |
| Empty | icon-tile 44 + `CardTitleText` title + `CaptionText` explanation, centred, inside a soft card; never an action the app cannot perform |
| Error | `ErrorNote`: ErrorSoft panel, `circle-alert`, bold title, one-line cause, then a `SecondaryButton` "Try again" wired to the existing retry command |
| Success | `SuccessNote`: SuccessSoft panel + `circle-check` + what happened + what happens next |
| Pending | `WarningNote` with `clock` + what is being reviewed + what the user can do meanwhile |

### 5.7 Navigation chrome

* Root screens: brand row (icon tile + "KaamLink" + role pill) — no app bar, no back.
* Detail screens: app bar 52dp — back affordance (44dp hit area), title in 15.5 semibold,
  optional single action icon at the end.
* Bottom action dock: 48dp button inside a 68dp surface with a 1dp top divider, used on
  forms where the primary action must stay reachable with the keyboard open.
* No flyout, no tab bar: the product's navigation is absolute Shell routes.

### 5.8 Motion

No custom animations. Default MAUI control feedback only (ripple on Android for buttons
and tappable rows). No page transitions beyond Shell defaults, no skeleton shimmer, no
parallax — deliberately, for low-end GPU cost and accessibility.

---

## 6. RTL rules (Arabic)

1. Never hard-code `HorizontalOptions="Start"/"End"` asymmetrically — use Start/End only,
   which MAUI mirrors automatically once `FlowDirection` follows the culture.
2. Set `FlowDirection` explicitly per page (`FlowDirection="MatchParent"` is the default and
   is correct in Shell when `Platforms/Android` sets `supportsRtl`); verify at runtime.
3. Progress indicators fill from the start (right) in RTL.
4. Numerals stay Latin (`1,800 – 2,500 AED`), which is standard for UAE hiring content.
5. Fixed two-column grids are allowed only for **numeric** pairs (salary min/max) and the
   2 × 2 decision grid; they mirror as a whole, not per cell.
6. A horizontal row of ≥3 action buttons is forbidden (this is what broke the old
   applicants screen in Arabic); use a wrapping/2-column grid.
7. Back arrows mirror automatically via the platform; the icon font glyph is not flipped
   manually.
8. Every Arabic string must exist in `AppStrings.ar.resx` before it can be rendered — the
   verifier enforces EN/AR key parity.

---

## 7. Responsiveness & accessibility

* Minimum supported width **320dp**; all layouts are single-column, wrapping stacks.
* Font scale: system scaling is respected. Text rows have no fixed heights; cards grow.
  Buttons keep a 48dp minimum height but may grow with their label.
* Touch targets ≥ 48dp for every actionable control (Material 48dp), including text
  buttons, danger buttons, pickers and the icon actions inside list rows.`Editor`s use a
  84dp minimum height so multi-line notes remain tappable and readable.
* Every screen: `SemanticProperties.HeadingLevel` on its title, `SemanticProperties.Description`
  on icon-only controls, `AutomationProperties.Name` on inputs.
* Colour is never the only status signal.
* Images: none — the app ships no raster assets beyond the icon/splash, so there is no
  image decoding cost on low-end devices.

---

## 8. Implementation map (token → XAML)

| Spec item | Implementation |
|---|---|
| Color tokens | `Resources/Styles/Colors.xaml` (keys named as in §1) |
| Type scale | `Resources/Styles/Styles.xaml` (`DisplayText` … `OverlineText`) |
| Buttons | `Styles.xaml` (`PrimaryButton`, `SecondaryButton`, `SubtleButton`, `DangerButton`, `DangerSolidButton`, `TextButton`) |
| Field label / hint / error | `Styles.xaml` (`FieldLabel`, `FieldHint`, `FieldErrorText`) |
| Cards / rows | `Styles.xaml` (`SurfaceCard`, `ListRow`, `DividerLine`) |
| Pills | `Styles.xaml` base + `Core/Presentation` converters for tone |
| Icons | `Resources/Fonts/lucide.ttf` + `IconGlyph` style + `Controls/IconView` |
| Notes (error/success/warning/info) | `Controls/NoticeBanner` + `NoticeKind` |
| Empty / loading | `Controls/StatePanel` + `StatePanelKind` |
| Section eyebrow | `Styles.xaml` `OverlineText` |
| Bottom dock | page-level `Grid` + `PrimaryButton` (no new control needed) |

Component controls are intentionally few (3) and property-only (no business logic), so
they cannot compromise MVVM.
