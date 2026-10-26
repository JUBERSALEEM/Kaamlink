# KaamLink UI Redesign — Implementation & Verification Report

Status: **implementation complete, static verification green, build/runtime verification BLOCKED (no SDK or device in
this environment).** A dedicated release-verification pass was run afterwards and is recorded in [§9](#9-post-fix-release-verification-gate);
it found and fixed nine defects — one of them a login blocker — and it does not change the
build/runtime verdict.
Branch: `arena/44c99984-kaamlink`. Base commit: `de54334`. Reports for the earlier phases:
`00-repository-and-screen-audit.md`, `01-mockup-catalog.md`, `02-design-system-specification.md`.

---

## 1. What this phase did

Every active screen in the app was rebuilt on one visual language, keeping the existing
architecture untouched: same routes and absolute navigation model, same view models,
same API client and DTOs, same resx localization system, same CommunityToolkit.Mvvm
commands. The redesign changed **presentation** (XAML, styles, tokens, shared controls)
and, where a bug was found, a small amount of **presentation-layer code** (derived
display properties, back-navigation commands, converters) — never an API contract,
domain rule, authentication flow or navigation architecture.

**15 of 15 active screens redesigned** (2 584 lines of XAML across the `Features` tree):

| # | Screen | File | Role | Primary job |
|---|---|---|---|---|
| 1 | Role selection (welcome) | `Features/Onboarding/RoleSelectionPage.xaml` | shared | choose worker / employer |
| 2 | Login | `Features/Auth/LoginPage.xaml` | shared | phone entry, role-aware |
| 3 | OTP | `Features/Auth/OtpPage.xaml` | shared | code entry, resend cooldown |
| 4 | Worker onboarding (3 steps) | `Features/Worker/WorkerOnboardingPage.xaml` | worker | profile, skills, pay/availability |
| 5 | Worker home (Skill Passport) | `Features/Worker/WorkerHomePage.xaml` | worker | completeness, skills, quick actions |
| 6 | Employer onboarding | `Features/Employer/EmployerOnboardingPage.xaml` | employer | company registration for review |
| 7 | Employer home | `Features/Employer/EmployerHomePage.xaml` | employer | companies + verification state |
| 8 | Vacancy feed | `Features/Jobs/VacancyFeedPage.xaml` | worker | search, filter, paginated results |
| 9 | Vacancy detail | `Features/Jobs/VacancyDetailPage.xaml` | worker | read + apply in one scroll |
| 10 | Vacancy editor | `Features/Jobs/VacancyEditPage.xaml` | employer | create / edit a vacancy |
| 11 | Company vacancies | `Features/Jobs/CompanyVacanciesPage.xaml` | employer | list + status actions |
| 12 | Applicants | `Features/Jobs/VacancyApplicationsPage.xaml` | employer | decide on applicants |
| 13 | My applications | `Features/Jobs/MyApplicationsPage.xaml` | worker | statuses + withdraw |
| 14 | My assessments | `Features/Assessments/MyAssessmentsPage.xaml` | worker | skill verification state |
| 15 | Assessment runner | `Features/Assessments/AssessmentRunPage.xaml` | worker | answer, submit, verdict |

### Design system shipped

* `Resources/Styles/Colors.xaml` — semantic tokens (`Primary #0B57A4` blue-led, restrained
  teal accent `#0B6660`, success/warning/error/info + soft variants, surface/border/text
  ramp), legacy aliases kept, 41 single-glyph Lucide icon keys.
* `Resources/Styles/Styles.xaml` — typography scale, icon styles, surfaces/cards, button
  hierarchy (primary / secondary / subtle / danger / text), inputs and field chrome,
  pills, layout helpers (`PageStack`, `FormStack`, `FieldStack`, `BottomDock`, `AppBar`),
  Shell chrome. No duplicate keys; every `StaticResource` in every page resolves.
* `Controls/StatusPill.cs`, `Controls/NoticeBanner.cs`, `Controls/StatePanel.cs` — three
  content-bound shared controls so status, banners and loading/empty states look and
  behave the same on all 15 screens. Status is always **glyph + label + tone**, never
  colour alone.
* `Core/Presentation/Converters.cs` — 12 converters (5 existed, 7 added), all 12
  registered in `App.xaml` (`InvertedBool`, `StatusColor`, `StatusSoftColor`,
  `VerificationStateColor`, `VerificationStateSoftColor`, `StringHasContent`,
  `CountPositive`, `CountEmpty`, `StageColor`, `StringEquals`,
  `SelectedOptionBackground`, `SelectedOptionText`).
* `Resources/Fonts/lucide.ttf` (vendored, MIT, licence file alongside) — real vector
  icons; **no emoji anywhere in the UI**.

### Brand surfaces aligned

Launcher icon background, splash background and Android `colorPrimary` moved from the old
teal/indigo values to the new blue, so the launcher and splash match the app.

---

## 2. Per-screen checklist (Phase 4 requirement)

Legend: **P** = satisfied and checked by an executed static check · **R** = reviewed by
reading the code (no runtime proof) · **B** = blocked (needs a device/build).

| Screen | Layout | Bindings | Commands | Navigation | Loading / error / empty / success | Localization | RTL | Accessibility | Small screen |
|---|---|---|---|---|---|---|---|---|---|
| Role selection | P | P | P | P | n/a (static screen) | P | P | P | R |
| Login | P | P | P | P | P (busy + error banner, back to roles) | P | P | P | R (keyboard, 48dp) |
| OTP | P | P | P | P | P (busy, error, cooldown chip) | P | P | P | R |
| Worker onboarding | P | P | P | P | P (loading, error+retry, step dock) | P | P | P | R |
| Worker home | P | P | P | P | P (loading, error+retry, empty skills) | P | P | P | R |
| Employer onboarding | P | P | P | P | P (review notice, busy, error) | P | P | P | R |
| Employer home | P | P | P | P | P (pending/verified/rejected per company) | P | P | P | R |
| Vacancy feed | P | P | P | P | P (loading, error+retry, empty, load-more) | P | P | P | R (virtualised list) |
| Vacancy detail | P | P | P | P | P (loading, error+retry, applied state) | P | P | P | R |
| Vacancy editor | P | P | P | P | P (loading, error+retry, sticky save) | P | P | P | R (list + form scroll) |
| Company vacancies | P | P | P | P | P (loading, error+retry, empty, status pills) | P | P | P | R |
| Applicants | P | P | P | P | P (loading, error+retry, empty, 2×2 actions) | P | P | P | R (2×2 not 4-across) |
| My applications | P | P | P | P | P (loading, error+retry, empty, withdraw gate) | P | P | P | R |
| My assessments | P | P | P | P | P (loading, error+retry, empty, unavailable note) | P | P | P | R |
| Assessment runner | P | P | P | P | P (loading, error+retry, progress, verdict) | P | P | P | R |

"Small screen" is **R**, not P, everywhere: no device or emulator was available, so 320–
360 dp behaviour is argued from the markup (single-column stacks, no fixed widths above
200 dp, wrapping labels, no fixed-height text rows) rather than measured.

---

## 3. Verification executed

All commands were run in this workspace, in this order, at the end of the work.

| Gate | Command | Result |
|---|---|---|
| Phase 3 (design system, tokens, routes, DI, DTO contract) | `python3 tools/verify-phase03.py` | **103 checks, 0 failures** |
| Phase 4 (jobs core) | `python3 tools/verify-phase04.py` | **105 checks, 0 failures** |
| Phase 5 (assessments + localization parity) | `python3 tools/verify-phase05.py` | **88 checks, 0 failures** |
| Compiled bindings (new) | `python3 tools/verify-bindings.py` | **347 bindings across 15 pages, 0 unresolved** |
| Redesign gate (new) | `python3 tools/verify-ui-redesign.py` | **350 checks, 0 failures** |
| XAML well-formedness | `xml.etree.ElementTree` parse of all 15 pages | all parse |
| Localization parity | resx/Designer inventory | **312 / 312 / 312** (EN / AR / Designer) |

> These are the counts as first measured. The gates were hardened afterwards (two rules were
> silently green — see §9.5 D1–D3), so the current counts are **349 compiled bindings** and
> **375 redesign checks**; §9.2 re-runs every gate with exit codes at the tip of the branch.

### What the new gates actually check

`tools/verify-bindings.py` resolves every `{Binding}` that XamlC would compile against the
declared `x:DataType` — page level, per `DataTemplate`, through
`Source={x:Reference}`, and through `BindingContext.X` — using type information scraped
from the C# sources (`public` properties, get-only collections, `[ObservableProperty]`
backing fields, `[RelayCommand]` methods, positional records). It also compares each
`DataTemplate`'s type with the item type of the collection it renders. It was validated by
injecting a typo and a template mismatch: both make it fail, and the typo injection was
reverted.

`tools/verify-ui-redesign.py` encodes the redesign rules: page contract (`x:DataType`,
`Title`, hidden Shell nav bar), no raw hex, no legacy style keys, no hardcoded Latin UI
text, no emoji, no text below 12sp, 48 dp action heights, a heading level per screen, error
/ empty / loading surfaces bound wherever `HasError` / `IsEmpty` / `IsBusy` are bound,
single-glyph icons, no duplicate token keys, no letter-spaced or uppercased section labels,
plus **feature parity**: every `[RelayCommand]` in every view model must still be bound
somewhere. It was validated by removing a command binding (the gate failed and named the
dropped command) and by reintroducing a hardcoded string (also failed).

### Bugs found and fixed during implementation (1–5 by the gates, 6–9 by review)

1. **Silent no-op buttons (runtime).** `MyAssessmentsPage` and `AssessmentRunPage` bound
   their item-level commands through
   `Source={RelativeSource AncestorType={x:Type vm:SomeViewModel}}`. A *view model type*
   never appears in the visual tree, so those bindings could not resolve: starting an
   assessment and selecting an answer would have done nothing. Both now use
   `Source={x:Reference Page}` with `BindingContext.Command`, the idiom used everywhere
   else.
2. **Vacancy save could not leave the form.** `VacancyEditViewModel` finished with
   `GoToAsync("..")`, a relative pop, while the app's navigation model is absolute
   (`GoToAsync("//route")`) and never builds a Shell stack. Save and the new Back action
   now navigate to the company vacancy list through the absolute route, carrying
   `companyId` and `companyName` so the list header stays correct.
3. **No way back from any secondary screen.** Every page hides the Shell nav bar for its
   own header, so the platform back arrow was gone. Each secondary screen (detail,
   applications, assessments, runner, company vacancies, applicants, editor) now has an
   explicit, localized 48 dp back action to its logical parent route.
4. **Failed assessment looked passed.** The verdict tile was hard-coded green with a check
   glyph; it now follows `ResultState` (soft tone + matching glyph).
5. **Keyboard could cover the sticky action dock.** `MainActivity` now declares
   `WindowSoftInputMode = AdjustResize`.
6. **The OTP screen told the user to go back and gave them no way to do it.** Its
   footnote says "Wrong number? Go back to edit it", but the page hides the Shell nav
   bar. That footnote is now the back action itself (`OtpViewModel.BackAsync` →
   `RoleNavigator.LoginRouteFor(Role)`), so the instruction and the control agree.
7. **Back actions scrolled away.** On the long screens (vacancy detail, editor,
   assessment runner, my applications, employer onboarding) the back action sat inside
   the scrolling content. It is now a pinned 52dp app bar row on every secondary screen.
8. **Feed showed verification as a text checkmark glued to the company name**
   (`"Gulf Steel Works ✓"`), which is not an icon, not localizable, and not readable as
   a state by a screen reader. The feed card now renders a `StatusPill` from the server's
   `CompanyVerificationState` and shows skills as chips. The redesign gate now fails on any
   text glyph (`✓ ✔ ★ ● ▪`) in a page or in a display string.
9. **Arabic-hostile section labels.** Section eyebrows were 10.5sp, uppercased and
   letter-spaced — uppercase means nothing in Arabic and letter spacing breaks Arabic
   joining. They are now 12sp semibold with no transform, and the design-system spec was
   corrected to match.

### Bug-fix pass (post-review audit)

A dedicated bug hunt after the redesign landed found eight defects. Two were
*pre-existing* logic bugs in the flows being restyled, two were introduced by the
redesign itself, and four were runtime-breakage risks in the markup/controls.

| # | Defect | Impact | Fix | Now guarded by |
|---|---|---|---|---|
| 1 | 14 item-level commands used `x:Reference` to the page **inside a `DataTemplate`** | XAML name scopes do not cross the template boundary; on .NET 10 explicit-source bindings inside `CollectionView` item templates are broken, so card taps, apply, withdraw, hire/reject, vacancy actions and remove-skill would do **nothing** | `Source={RelativeSource AncestorType={x:Type ContentPage}}` | gate rule J |
| 2 | `StatusPill` assigned the **Color** token `Border` to `Border.Stroke` (a `Brush` property) | Wrong type on a bindable property; stroke unreliable | Uses `BorderBrush`; every `Stroke=` in the app now takes a `*Brush` token | gate rule J |
| 3 | `StatusPill` never ran its tone logic at construction | Any pill without a `Tone` (worker-home skill states) rendered as **bare text** — no outline, no fill | `ApplyTone()` in the constructor | gate rule J |
| 4 | Feed advanced its page cursor **before** the request | A failed "load more" silently skipped a page of jobs forever | Cursor advances only after a successful response | — (logic) |
| 5 | Search cleared the list before checking `IsBusy` | Tapping search during an in-flight load wiped the results and the in-flight page then repopulated them from the old query | Guard first, clear second | — (logic) |
| 6 | Feed error surface lived inside `CollectionView.EmptyView` | A failure after results existed was **invisible** (EmptyView only renders when empty) | Banner + retry moved to the pinned header | — (logic) |
| 7 | Three mutation flows checked `IsBusy` but never set it (pre-existing) | Double-tap sent **duplicate** hire / reject / withdraw / status-change requests | Private `_isWorking` latch (not bound to UI, so no spinner flash) | — (logic) |
| 8 | Company-vacancy header rendered an empty heading without `companyName` (pre-existing) | Deep link / cold stack showed a blank title | `HasCompanyName` drives the heading or the page's own name | gate rule: converter/type |

Two gate defects were fixed as part of this pass:

* `verify-bindings.py` treated `Path=X` as a positional path and could not map
  `AncestorType=ContentPage` back to the page view model, so after fix 1 it silently
  **skipped** all 14 bindings. It now parses named arguments properly and resolves
  `ContentPage` to the page VM; a deliberate typo in one of them fails the gate again.
* The first attempt at fix 8 bound `InvertedBoolConverter` to a **string** property.
  That converter returns `value is not true`, which is always `true` for a string, so
  the fallback heading would have rendered permanently. Caught before commit, and the
  gate now type-checks the input of every bool/count/string converter it knows.

### What the gates do **not** prove

They are static text/XML analyses. They cannot prove the app compiles, that XAML property
values are type-correct at runtime, that fonts load, that layouts look right, or that
network flows behave. Those need a build and a device (see §4).

---

## 4. Build and runtime status — honestly reported

| Item | Status | Evidence |
|---|---|---|
| .NET SDK build (`dotnet build -f net10.0-android`) | **BLOCKED** | `command -v dotnet` → not found; no SDK in this sandbox, and the install hosts (`dot.net`, `aka.ms`) are outside the allowed network egress |
| Java / Android SDK | **BLOCKED** | `command -v java` → not found; `ANDROID_HOME` unset |
| Emulator / physical device run | **BLOCKED** | no SDK, no emulator, no device |
| Screenshot comparison against mockups | **NOT PERFORMED** | requires a running app; the 23 mockups in `docs/ui-redesign/mockups/` remain the visual reference, not a proof of the built UI |
| XAML well-formedness | PASS | all 15 pages parse as XML |
| XamlC binding resolution | PASS (static) | `tools/verify-bindings.py`, 347 bindings |

No build was attempted and failed for a code reason; the toolchain is simply absent. **No
claim of a passing build or a working app is made anywhere in this report.**

To verify on a machine with the SDK:

```bash
dotnet build KaamLink.csproj -f net10.0-android -c Release
# then, with an emulator or device attached:
dotnet build KaamLink.csproj -t:Run -f net10.0-android
```

Expected first-run checks: font assets (OpenSans + `lucide.ttf`) load; no XFC0045 binding
warnings; each screen's loading/empty/error state appears on a cold start with the API
down; the device in Arabic (`ar-AE`) mirrors layouts.

---

## 5. Localization and RTL

* **No hardcoded English** in any page: every user-visible string is an `x:Static`
  `AppStrings.*` reference (the redesign gate fails on Latin text in `Text`/`Title`/
  `Placeholder`/`Message`/`Description`).
* **Parity**: 312 keys in `AppStrings.resx`, `AppStrings.ar.resx` and
  `AppStrings.Designer.cs`. The redesign added **122 keys** (190 → 312), each with English
  and Arabic copy written for the new layouts; **no existing key, format string or
  placeholder was changed** (verified by diffing the resx against the base commit:
  123 added, 0 removed, 0 modified).
* **Real RTL**, not just right alignment: `android:supportsRtl="true"` in the manifest, no
  hardcoded `FlowDirection="LeftToRight"` anywhere, layouts built from `Grid` columns and
  start/end padding so mirroring is automatic, no directional icon used as an affordance
  (the card chevron was removed; back is a word, not an arrow), no uppercase or letter
  spacing anywhere, no fixed-width text containers.
* **Known limitation**: the bundled Latin face is Open Sans; Arabic glyphs fall back to the
  platform face (Noto Naskh on Android). This is correct rendering but means Arabic
  typography is not brand-controlled. Bundling an Arabic-capable face and switching it per
  culture is the follow-up (it touches every text style, so it was not done inside a
  visual-only pass).
* **No in-app language switcher** — the app follows the device locale, as before.

---

## 6. Deviations, known issues, and what was deliberately not changed

| Item | Note |
|---|---|
| Mockup eyebrow style | Mockups were drawn before the Arabic-safety correction; they show 10.5sp uppercase letter-spaced eyebrows, the app ships 12sp semibold. Spec §2 records this. |
| Light theme only | The app had no dark theme before and still has none (`AppThemeBinding` count: 0). Adding one is a design-system expansion, not a screen fix. |
| Save-vacancy navigation | Changed from a relative pop to the absolute company-vacancy route (see §3.2). Same destination, same intent, now actually reachable. |
| Back navigation | Added as explicit in-page actions. The routing model (absolute routes, no stack) is untouched. |
| Edit re-asks for skills | Pre-existing, documented V1 limitation: the employer vacancy list endpoint does not return skill ids, so the editor requires a fresh selection rather than silently clobbering. Preserved as designed. |
| Feed result count | The old page bound the raw collection count (`Items.Count` with `StringFormat='{0}'`) as a bare number. It is now a localized "Results" label above the list; no count is invented. |
| Verification pill on feed | The old feed appended "✓" to the company name; the card now shows the localized "Verified company" pill only when the server reports `Verified`. No unverified employer is ever badged. |
| Removed pagination button state | The feed keeps `LoadMoreCommand` behind `HasMore`, unchanged in behaviour, restyled as a secondary button plus a footer note. |
| Amber `colorAccent` | Android's stale amber accent is now the teal accent token so platform widgets do not flash a third colour. |
| No skeletons/shimmer | Loading uses spinners + a labelled panel. Decided against decorative shimmer for low-end performance and honesty (no fake content shapes). |
| No tab bar / notifications / chat / saved jobs | Not in the existing product; not added (source of truth is the repository). |

---

## 7. Acceptance criteria

| Criterion | Status | Evidence / exact gap |
|---|---|---|
| Full-screen inventory documented before any XAML change | **PASS** | `docs/ui-redesign/00-repository-and-screen-audit.md`, committed before the first page edit |
| Mockups for every screen and key state + catalogue + design-system specimen | **PASS** | 23 PNGs in `docs/ui-redesign/mockups/` (generated, not hand-waved), `01-mockup-catalog.md` mapping mockup → real file, `02-design-system-specification.md`; internal review gate run on 6 contact sheets and fixed before implementation |
| One coherent visual language, semantic tokens, no duplicate keys, no broken resource references, no raw hex in pages, no emoji icons | **PASS** | tokens in `Colors.xaml`/`Styles.xaml`; `verify-phase03` (declared keys), `verify-ui-redesign` (no raw hex, no emoji, no duplicates) |
| Status patterns never colour-only | **PASS** | `StatusPill` always renders glyph + label; all pills in all pages use it |
| All 15 active screens redesigned | **PASS** | §1 table; `verify-ui-redesign` page contract applies to all 15 |
| No working feature removed | **PASS** (static) | command-reachability check (every `[RelayCommand]` bound); diff of bindings/commands vs base commit shows only the two `DataContext.→BindingContext.` idiom fixes and one obsolete retry gate replaced by an error banner |
| No hardcoded fake data or fake success | **PASS** | every displayed state comes from the API response via the existing view models; new derived properties are pure formatting/presentation |
| No API/DTO/auth/domain change for visual reasons | **PASS** | DTOs, routes, auth flow and domain rules untouched; `verify-phase03` asserts the DTO surface against the backend contract. The verification pass edited only the *error* path of `Core/Api/ApiClient.cs` (timeout/malformed-body handling) — no wire format change (§9.5 D6) |
| EN/AR parity, all new text localized | **PASS** | 312/312/312, `verify-phase05` parity check + redesign gate's hardcoded-text check |
| Real RTL | **PASS** (static) | §5; hardware/emulator confirmation is BLOCKED |
| Accessibility: 48 dp targets, headings, semantic labels, ≥14sp body | **PASS** (static) | redesign gate: button/nav heights, heading per screen, label/description presence; body 14.5sp |
| Small screens, font scaling, keyboard-open forms | **PARTIAL** | markup designed for it (single column, no fixed text heights, `AdjustResize`, `MinimumHeightRequest` on editors) and reviewed, but **not measured on a device** — no SDK/emulator |
| Low-end performance | **PARTIAL** | feed converted from a `ScrollView` of every card to a virtualised `CollectionView`; no raster images; no per-card shadows/animations. Not profiled on hardware |
| API 21 minimum respected | **PASS** | `SupportedOSPlatformVersion=21.0` unchanged (`verify-phase03`) |
| Existing verifiers and tests run | **PASS** | 103 / 105 / 88, 0 failures, plus two new gates |
| Real Android build attempted and reported honestly | **BLOCKED** | no .NET SDK / Java / Android SDK in this environment; §4 states this explicitly and gives the exact commands |
| Screenshot comparison with mockups | **BLOCKED** | requires a running app; mockups remain the reference |
| Final report with PASS / PARTIAL / BLOCKED per item | **PASS** | this table |

---

## 8. Files changed (summary)

```
App.xaml                                    12 converter resources registered
Controls/{StatusPill,NoticeBanner,StatePanel}.cs        new shared controls
Core/Presentation/Converters.cs            +7 converters (12 total, 5 pre-existing)
Features/**/*.xaml                         15 screens redesigned (2 584 lines)
Features/**/*ViewModel.cs                  9 files: derived display state, back commands,
                                           companyName routing, answered progress
Resources/Styles/{Colors,Styles}.xaml      design system v1.0 (rewritten)
Resources/Strings/*                        122 new EN/AR keys (190 -> 312), 0 changed
Resources/Fonts/lucide.ttf + licence       vendored icon font
MauiProgram.cs                             Lucide font registration
Platforms/Android/MainActivity.cs          AdjustResize
Platforms/Android/Resources/values/colors.xml, KaamLink.csproj, AppIcon, Splash
                                           brand surfaces re-coloured to the new blue
Core/Text/DigitText.cs                     Arabic-Indic → ASCII digit normalization (§9.5 D5)
Core/Api/ApiClient.cs                     transports timeout/malformed-body failures as ApiException
tools/{verify-bindings,verify-ui-redesign}.py          new gates (349 bindings / 375 checks)
tools/verify-gates-selftest.py             17 injected defects, all must be caught (§9.5)
tools/add-strings.py                       resx + Designer + AR writer used for every new key
docs/ui-redesign/00,01,02,03               audit, mockup catalogue, design system, this report
```

Commits on `arena/44c99984-kaamlink` (descriptive subjects, oldest first; the branch is
never force-pushed and each commit is a self-contained, verifier-green group):

```
UI redesign 1/2: design system, tokens, Lucide icons, shared controls, audit + mockups + docs
UI redesign 2/2: all 15 screens rebuilt, explicit back navigation, a11y/type pass,
                 static binding + redesign gates, implementation report
UI redesign: pinned back actions, OTP escape hatch, data-driven verification badge
UI redesign: avoid pluralised result counts, drop the unused format key
UI redesign: fix 8 defects found in the post-review bug hunt (see the table above)
a11y: announce tappable cards and quick actions to screen readers
fix: accept Arabic-Indic digits, harden API failures, unify auth errors
gate: prove tap-target semantics and digit handling; add a negative-test harness
docs: post-fix release verification results (this report)
```

The redesign work is split into two reviewable commits because a mid-work environment
reset re-cloned the repository and dropped the earlier fine-grained history; no content was
lost — every file below is present and verified at the tip of the branch.

---

## 9. Post-fix release verification gate

This section was produced by a dedicated verification pass over the finished redesign. Its
scope was **verify and fix, not redesign**: no screen was restyled, no route or DTO changed,
and nothing below is copied from an earlier section — every table entry comes from a command
that was executed in this workspace against the tree described here.

Verification started at `7daf6af` (the tip of the bug-fix pass); the defects it found are fixed
in the commits below, and the report itself was written mid-pass (case D9 was found after
`c8be061`, which is why it appears as a later fix rather than in the first table row):

| Commit | Subject |
|---|---|
| `c4b9486` | a11y: announce tappable cards and quick actions to screen readers |
| `dd88869` | fix: accept Arabic-Indic digits, harden API failures, unify auth errors |
| `5c65a14` | gate: prove tap-target semantics and digit handling; add a negative-test harness |
| `c8be061` | docs: post-fix release verification results (this section) |
| `a8c7517` | fix: decode the phone query parameter (OTP verification was blocked) |
| `067d06a` | gate: require escaped route values to be decoded by their consumer |

### 9.1 Git state — checked, not assumed

```
$ git rev-parse --abbrev-ref HEAD          arena/44c99984-kaamlink
$ git rev-parse HEAD                       067d06a25ecac88d135842f393b52d0d746fcef1
                                              (the commit the gates below were run against;
                                               this document is the next commit)
$ git merge-base --is-ancestor de54334 HEAD   -> true (base is an ancestor)
$ git rev-list --count de54334..HEAD       11   (5 redesign + 5 verification + this section)
$ git status --porcelain                   clean before each commit
$ git rev-list --left-right --count HEAD...origin/arena/44c99984-kaamlink   0  0 after the final push
$ git diff --shortstat de54334..HEAD       87 files changed, 9510 insertions(+), 1152 deletions(-)
```

Diff audit, per area (looking for unintended edits, missing files and business-logic drift):

| Area | Finding |
|---|---|
| `Features/**/*.xaml` | 15 screens; the two superseded originals (`JobFeedPage`, `Worker/{MyApplications,MyAssessments}Page`) are still on disk but are **not** referenced by `AppShell.xaml` or DI |
| `AppShell.xaml` | 15 `ShellContent`, routes byte-identical to `AppRoutes` (15 constants); no route added, renamed or reordered |
| `Core/` | only `Converters.cs` (+7 converters) and this pass's `ApiClient.cs` error handling; navigation, DTOs (`ApiContracts.cs`), session and JSON context untouched |
| `MauiProgram.cs` | adds the Lucide font name; page/VM registrations unchanged, no duplicate `AddTransient` (checked by `uniq -d`) |
| `KaamLink.csproj` | app icon / splash tint to the brand blue; `TargetFrameworks`, `SupportedOSPlatformVersion=21.0` and package versions unchanged |
| `Platforms/Android` | `MainActivity` `WindowSoftInputModeAdjust.Resize`; `supportsRtl="true"`; brand color resource |
| Binary additions | exactly two: `Resources/Fonts/lucide.ttf` and the 23 mockup PNGs. No build output, no coverage reports |
| Backend | untouched by the FLUTTER/MAUI work; the pass only *read* `backend/src` to confirm wire contracts |

No history rewrite, reset or force-push was performed; the branch only ever moved forward.

### 9.2 Gates re-run (commands, exit codes, real output)

Every gate was run with its exit code captured (`out=$(cmd); code=$?` — piping to `tail` would
report `tail`'s status instead):

| # | Command | Exit | Output |
|---|---|---|---|
| 1 | `python3 tools/verify-phase03.py` | **0** | `103 checks, 0 failures — ALL STATIC CHECKS PASSED` |
| 2 | `python3 tools/verify-phase04.py` | **0** | `105 checks, 0 failures — ALL STATIC CHECKS PASSED` |
| 3 | `python3 tools/verify-phase05.py` | **0** | `88 checks, 0 failures — ALL STATIC CHECKS PASSED` |
| 4 | `python3 tools/verify-bindings.py` | **0** | `15 pages, 314 types, 349 compiled bindings checked` |
| 5 | `python3 tools/verify-ui-redesign.py` | **0** | `381 checks, 0 failures — ALL REDESIGN CHECKS PASSED` |
| 6 | `python3 tools/verify-gates-selftest.py` | **0** | `all 18 injected defects were detected; tree restored clean` |
| 7 | `python3 tools/add-strings.py --check` | **0** | `OK — en=312 ar=312 designer=312` |

**Why the counts moved** (350 → 381 redesign checks, 347 → 349 bindings). Nothing was dropped;
the new rules are additive, and the gate was hardened *after* §3 was written:

* +16 for a strict `x:DataType` rule and an escape-aware glyph scan (§9.5 D1/D2);
* +5 for the tappable-element semantics rule (D4);
* +4 for the typed-credential rule (D5);
* +6 for the escaped-query round-trip rule (D9);
* +2 bindings from the two `SemanticProperties.Description="{Binding …}"` added in `c4b9486`,
  +1 type because `DigitText` is now a scannable type.

Localization was additionally re-checked with an independent script (not the repo's own
tools): **312 EN / 312 AR / 312 Designer**, no orphans, no empty values, every AR value
contains Arabic characters, every `{0}`/`{1}` placeholder set matches between EN and AR.

### 9.3 Previously reported bugs — traced end to end

A binding or command being *present* was not accepted as proof. Each row was traced through
source, state mutation and navigation result.

| # | Journey step | What was verified (source-level) | Verdict |
|---|---|---|---|
| 1 | **Start an assessment** | `MyAssessmentsPage` card command → `OpenAssessmentAsync` builds `assessmentrun?skillId=…&skillName=…`; `AssessmentRunViewModel.ApplyQueryAttributes` parses `skillId`, decodes `skillName`, and calls `StartAsync()` once (`_attemptId == Guid.Empty` guard). `StartAsync` → `StartAssessmentAsync`, stores `AttemptId`, sets pass mark, materialises questions ordered by `SortOrder` with 4 localized options and `IsLoaded` — the page shows the quiz only when `IsLoaded && !HasResult` | **PASS** |
| 2 | **Select an answer and have it persist** | `SelectOption` sets `IsSelected` on exactly one option per question and records `SelectedIndex`; `SubmitCommand` is `CanExecute = CanSubmitAll`, which requires *every* question answered; submit maps `q.SelectedIndex!.Value` in question order. Options are re-created per attempt, so no stale selection can survive | **PASS** |
| 3 | **Save a vacancy and leave the editor** | `SaveAsync` (after `dd88869`) normalizes digits, parses once, validates, then sends those exact values; new vs edit chosen by `_vacancyId == Guid.Empty`; success → `ReturnToListAsync()` → absolute `companyvacancies?companyId=…&companyName=…`, the same route that produced the page, so the list reloads from `OnAppearing` | **PASS** |
| 4 | **Return from every secondary screen** | Scripted audit of every page that exposes a `BackCommand`: 11 targets resolve to a registered route constant. `Otp → RoleNavigator.LoginRouteFor(Role)` (role preserved), `AssessmentRun → MyAssessments`, `VacancyDetail/…` → parents, editor/applicants → `CompanyVacancies` with both query keys. `WorkerOnboarding` backs up by decrementing `Step` and hides Back on step 1 — correct, not a dead button | **PASS** |
| 5 | **OTP → back to fix the phone number** | `OtpViewModel.BackAsync` → `RoleNavigator.LoginRouteFor(Role)` → `login?role=worker\|employer`; `LoginViewModel.ApplyQueryAttributes` restores the role chip, and the `OnDisappearing` hook disposes the cooldown timer so a re-entry starts clean rather than stacking timers | **PASS** |
| 5b | **Login → OTP hand-off (found by this gate)** | `LoginViewModel.SendCodeAsync` normalizes the number, calls the API, then routes `OtpRouteFor(phone, role, cooldown)` which escapes the value (`?phone=%2B971…`). Shell delivers query values to `IQueryAttributable` **undecoded**, so before `a8c7517` the OTP screen held `%2B971501234567`, showed it to the user, and sent it to `VerifyOtpAsync` — every login would have failed phone validation. Now decoded on receipt like every other escaped route value | **PASS after `a8c7517`** |
| 6 | **Failed vs passed result** | `ResultPassed` drives two distinct icons (`IconCheckCircle` / `IconX`), two localized titles, two notes, and `ResultState` = `AssessmentVerified` / `Rejected` feeds the color converters — both token names exist in both tables. Score line is `{correct} of {total}` + percent, server-provided | **PASS** |
| 7 | **Employer verification from real data** | `EmployerHomeViewModel` reads the companies from `GetMyCompaniesAsync`; each card carries the server's `VerificationState` string, its localized label via `EnumDisplay.VerificationState`, `IsPending`, and a reason line only when the API returned one. Three banners are bound to `Pending` / `Verified` / `Rejected` via `StringEquals`. Wire values confirmed against the backend, which projects `company.VerificationState.ToString()` into a `string` DTO field — so the client's enum-name literals match exactly (and the API never emits an ordinal) | **PASS** |

### 9.4 Regression sweep across all 15 screens

| Area | Method | Result |
|---|---|---|
| Dead buttons | every `[RelayCommand]` in every VM must be bound in XAML or code-behind (gate rule I) | 0 unbound |
| `RelativeSource` / template bindings | all 14 item-template command bindings use `{Binding BindingContext.X, Source={RelativeSource AncestorType={x:Type ContentPage}}}` + `CommandParameter={Binding .}`; no `x:Reference` in a template | 0 violations |
| Routes | 15 `AppRoutes` constants == 15 `ShellContent` == 15 DI page registrations; producer/consumer matrix of every query key | 0 mismatches (`vacancyId` optional = new vs edit) |
| Broken routes from buttons | every `GoToAsync` argument resolved to a constant (balanced-paren extraction, multi-line interpolations included) | 0 unresolved |
| Loading / empty / error states | `OnAppearing` load trigger present on all 9 list/detail pages; `IsBusy`/`IsEmpty`/`HasError` surfaces bound where the VM exposes them | present on all |
| Async commands | no `async void`; every mutation latched (`IsBusy` guard + `finally`); no `_isWorking`-less double-tap window | 0 found |
| Resource keys | 153 declared keys cross-referenced against `StaticResource`/`DynamicResource`/`SetDynamicResource`/`Resolve` | 0 unresolved |
| Localization | parity script above + gate's hardcoded-Latin-text rule | PASS |
| RTL | `android:supportsRtl="true"`; no `FlowDirection` override anywhere; no `HorizontalTextAlignment="Left/Right"`; only `HorizontalOptions="Start"` (20) and `Center` (26) | PASS (static) |
| Status mappings | every `ApplicationStatus`, `VacancyStatus` and skill state value mapped in both color tables; names verified against the backend enums | complete |
| Validation | phone (E.164 mirror), OTP (6 ASCII digits), display name, emirate, profession, ≥1 skill, years 0–50, duplicate skill, salary min/max/order, availability, headcount, company name/licence | all guarded client-side, server authoritative |
| Persistence | every save path awaits the API before navigating; failures keep the user on the form with a localized message | PASS |
| API contract | no DTO, route or auth change; contracts compared field-by-field with `backend/src/KaamLink.Application` records | unchanged |
| Hardcoded demo data | grep for seeded names/phones/amounts and a scan of every collection initialiser | none (only the static emirate filter list) |
| Encapsulation | no service locator in the app, no duplicate DI registration, no `async void`, code-behind ≤ 25 lines per page (constructor + `OnAppearing` + OTP timer disposal) | PASS |
| Timer lifecycle | `OtpPage.OnDisappearing` → `StopTimer()` → disposes `IDisposable` timer | PASS |

### 9.5 Defects found by this gate, and fixed

**D1 — a gate that was silently green (`x:DataType`).** The page-contract rule accepted any
non-empty `x:DataType`, so `x:DataType="{x:Null}"` passed while compiled bindings were
disabled for the whole page. Now the value must contain `:` and must not be `{x:Null}`.

**D2 — a gate that only looked for literal characters.** Icon-glyph scanning matched `✱` but
not `"\u2605"` in C# or `&#x2605;` in XAML, which render identically. The scan now covers all
three spellings and all `Features/**/*.cs`.

**D3 — the a11y rule this gate added was itself vacuous.** It matched the bare tag
`GestureRecognizers`, but MAUI writes the property element `<Border.GestureRecognizers>`, so
the rule never fired. Caught by the new negative-test harness, not by review
(a 371-check "pass" that verified nothing).

**D4 — five tappable elements with no accessible name.** The three worker-home quick-action
tiles, the vacancy-feed card and the assessment option were `Border`s with a
`TapGestureRecognizer` and no `SemanticProperties.Description`: a screen reader announced an
unlabelled element with no hint that it was actionable. All five now carry a description,
and the gate fails if a future tap target omits one.

**D5 — valid Arabic numerals were rejected (localization defect).** The app ships an Arabic
RTL experience, and Arabic-locale numeric keyboards emit Arabic-Indic (`٠١٢٣`) or Extended
Arabic-Indic (`۰۱۲۳`) digits. `DigitText` did not exist, `PhoneInput` matched ASCII only,
`OtpViewModel` validated with `char.IsDigit` (true for Arabic-Indic) while the API rejects
anything non-ASCII, and `int.TryParse` failed for salary, years and headcount. Net effect: an
Arabic user could not log in with a correct phone number, and a valid code was sent and
rejected. `Core/Text/DigitText.cs` now maps any Unicode decimal digit to ASCII at the five
input paths; the wire format is unchanged, the API contract is untouched. Logic verified by
porting the routine and asserting 10 input/output cases (ASCII, both Arabic digit blocks,
mixed text, `+971` prefixes).

**D6 — slow networks crashed the app instead of showing the offline banner.** `ApiClient`
wrapped `HttpRequestException` but not `TaskCanceledException`, which is what `HttpClient`'s
own 100-second timeout throws; no view model catches `OperationCanceledException`, and the
`catch` clauses are `ApiException`-only. A malformed or empty `2xx` body had the same shape
(`JsonException`, and a literal `null` body yielding `null` that the caller dereferences).
All three are now converted to `ApiException` at the single choke point; a *genuine* caller
cancellation still propagates.

**D7 — the editor validated one parse and persisted another.** `SaveAsync` ran `int.TryParse`
for validation and again for the request payload, so the two could disagree; it now parses
once. Same routine also gained digit normalization (D5).

**D8 — generic error for every OTP failure.** The backend emits `auth.otp_locked`,
`auth.otp_resend_cooldown`, `auth.otp_not_found_or_expired` and `auth.phone_invalid`; the
client mapped only `auth.otp_invalid` and `rate.limit_exceeded`, so a locked-out user was
told "something went wrong". `LoginViewModel.MapError` is now the single auth table and maps
all of them to existing localized strings (no new keys, no new wording).

**D9 — the phone number was never decoded, so login could not complete (blocker).** The MAUI
documentation is explicit that query-string values are URL-decoded *only* when they arrive via
`QueryPropertyAttribute`; pages implementing `IQueryAttributable` receive the raw text. The
codebase already knew this — `companyName`, `skillName` and `title` are all escaped by their
producer and unescaped by their consumer — but `OtpViewModel` assigned `%2B971501234567`
straight to `Phone`. That value is displayed as "code sent to …" and passed to
`VerifyOtpAsync`, so **every** OTP verification would have failed validation and no user could
have signed in. Fixed in `a8c7517`; the gate now enforces the producer/consumer round trip for
every escaped query key (D9 rule, +6 checks), with an 18th injected-defect case.

**Gate harness added.** `tools/verify-gates-selftest.py` injects 18 realistic defects one at a
time (typo'd binding, typo'd command, dropped command binding, hardcoded text, emoji icon,
`\u2605` in C#, `&#x2605;` in XAML, multi-glyph icon key, `x:Reference` in a template,
Color-on-`Stroke`, wrong converter type, undeclared resource key, missing AR translation,
`x:DataType="{x:Null}"`, a skeleton with no `x:DataType`, a tappable element with no
description, the un-decoded `phone` parameter, and both digit-normalization regressions), runs
the responsible gate, and requires a non-zero exit. Every mutation is applied to a temporary
copy and restored; the harness prints the restored-clean state. **Result: 18 / 18 detected.**

### 9.6 Android build — BLOCKED (exact dependency list)

| Probe | Output |
|---|---|
| `command -v dotnet` | not found (`dotnet --info` → `command not found`) |
| `command -v java javac` | not found |
| `ANDROID_HOME`, `ANDROID_SDK_ROOT` | unset; no `/usr/lib/android-sdk`, `~/Android/Sdk`, `/opt/android-sdk` |
| `command -v adb emulator` | not found |
| `~/.dotnet`, `~/.nuget/packages` | absent (no SDK, no restored packages) |
| `curl https://dot.net`, `builds.dotnet.microsoft.com`, `dl.google.com`, `api.nuget.org` | `000` (egress blocked; only GitHub/npm/PyPI are reachable) |
| `apt-cache policy openjdk-17-jdk-headless` | not available to uid 1001 (no root) |
| `global.json` | pins SDK `10.0.100`, `rollForward: latestFeature` |

So the build is blocked by **absent toolchain that cannot be downloaded from this sandbox**,
not by a code failure. Nothing was attempted and then reported as passing. Required on a
build-capable machine: .NET SDK 10.0.1xx+ (`global.json` gate), the `maui-android` workload,
JDK 17, Android SDK with `platforms;android-36` + `build-tools;35.0.0` + `platform-tools`.

```bash
# reproducible build (from the repository root)
dotnet --version                      # must be >= 10.0.100 (global.json)
dotnet workload install maui-android
dotnet restore KaamLink.csproj
dotnet build KaamLink.csproj -f net10.0-android -c Release

# deploy + smoke test (emulator or device attached)
adb devices
dotnet build KaamLink.csproj -t:Run -f net10.0-android
adb logcat -s DOTNET:V mono-rt:V AndroidRuntime:E      # watch for XFC0045 / MAUIG warnings
```

The first build is also the first real XamlC pass: `verify-bindings.py` approximates the
compiler, but only XamlC can confirm there are no `XFC0045` errors. Treat any `XFC0045`
warning as a defect in this redesign.

### 9.7 Runtime, device and screenshot verification — BLOCKED

No SDK, no emulator, no attached device, and no way to obtain them here, so **no runtime claim
is made anywhere in this report**. Static analysis is not runtime success. Specifically still
unproven: whether the app launches; whether the two journeys complete against a live API;
every back action; keyboard behaviour on forms; TalkBack traversal order; font scaling; RTL
mirroring; and any pixel-level comparison with `docs/ui-redesign/mockups/`.

For a machine with a device, run this script and attach the screenshots — it is the shortest
path to converting the BLOCKED/PARTIAL rows below into PASS:

1. cold start with the API unreachable → every screen shows its error state, never a blank page;
2. worker journey: role → login → OTP (**type the code with an Arabic keyboard** → must be
   accepted) → onboarding 3 steps → home → feed (search, filter, paging, empty result) →
   vacancy detail → apply → my applications → withdraw;
3. employer journey: onboarding → home (Pending banner) → editor (create, **edit**, invalid
   salary) → company vacancies (status actions) → applicants (shortlist/hire/reject);
4. assessments: start, answer, submit, pass and fail verdicts, done;
5. back from every secondary screen, plus a mid-form rotation;
6. `ar-AE` + RTL, 320 dp width, 200 % font scale, TalkBack on the five tap targets fixed in `c4b9486`;
7. screenshots of all 15 screens in EN and AR.

### 9.8 English/Arabic and RTL coverage

* **Content:** 312 / 312 / 312 with matching placeholders — **PASS** (executed script).
* **No untranslated UI text:** gate rule over every XAML file — **PASS**.
* **Digits:** the `dd88869` fix removes the last Arabic-input hole found statically — **PASS (static)**.
* **Mirroring:** `supportsRtl=true`, no forced `FlowDirection`, no absolute left/right
  alignment, `Start`-based alignment only, icon+text rows built as `Grid`s that mirror as a
  unit — **PASS (static)**; the mirrored *result* is **BLOCKED** (no device).
* **Server-supplied text:** rejection reasons are admin free text and will appear in whatever
  language they were written in. That is a content-process gap, not a client defect, and it is
  listed as a risk below.

### 9.9 Accessibility coverage

| Item | Status |
|---|---|
| 48 dp minimum for every actionable element (`Button` heights, icon tiles, nav targets) | PASS (gate) |
| Semantic description on every tap target | PASS (gate rule added this pass) |
| One heading per screen, `Level1` screen title, `Level2` card titles | PASS (gate) |
| Status is never colour-only (pill = glyph + label) | PASS |
| Body text ≥ 14.5 sp, captions ≥ 12 sp, no letter-spaced uppercase | PASS (gate) |
| TalkBack traversal, focus order, large-font reflow, contrast measurement | BLOCKED (needs device/TalkBack) |

### 9.10 Remaining risks

| # | Risk | Impact | Mitigation |
|---|---|---|---|
| R1 | No compilation and no runtime execution in this environment | A XAML/C# error or a runtime-only binding failure is still possible | run the §9.6 build and §9.7 script before release; `dotnet build` warnings are the first signal |
| R2 | Gate approximation vs XamlC | `verify-bindings.py` infers types from source, so it can miss compiler-only diagnostics (`XFC0045`, `MAUIG1001`) | treat first real build as a release gate |
| R3 | `CompanyVerificationState` also defines `Suspended` and `Expired`; the client maps them to a neutral pill and shows the **raw enum name** | A future backend that starts emitting those states would show untranslated English and no explanatory banner | the current backend never assigns them (verified: only `Verify`/`Reject` transitions); add EN/AR keys and a banner when that changes |
| R4 | Admin-entered rejection reasons are free text | English text can appear inside the Arabic UI | content/ops process; not a client defect |
| R5 | 66 backend error codes, 13 mapped | unusual failures surface as the localized generic message | intentional (`Detail` is never shown); extend `ApiClient.Codes` + `MapError` as needed |
| R6 | Assessment abandonment leaves a server-side attempt open | the backend decides its lifetime; the client shows the next state from the API | verified the UI reports `assessment.not_available` / `already_completed` correctly |
| R7 | `ApplyQueryAttributes` starts the assessment with `_ = StartAsync()` | any exception type not derived from `ApiException` inside the task would be unobserved | `StartAsync` wraps its whole body in `try/finally` and all API failures now surface as `ApiException` (D6) |
| R8 | A **Release** build has no real endpoint: `ApiConfig.BaseUrl` is `https://replace-me-when-environments-exist.kaamlink.invalid/` (DEBUG uses `http://10.0.2.2:8080/`, and `network_security_config.xml` allows cleartext to the emulator loopback only) | a Release APK cannot reach any API, so it is not shippable regardless of the UI work | hosting/staging is explicitly a later phase; a release candidate needs the real base URL first |
| R9 | A 401 whose silent refresh fails for a *transient* reason (offline, 5xx) still surfaces as 401, so the view model routes to the welcome screen | the user appears signed out on a network blip (tokens are kept, so a restart recovers) | pre-existing session contract (`RefreshAsync` returns a bool); distinguishing "session over" from "could not refresh" needs an `ISessionService` change and was deliberately out of scope for a UI pass |

### 9.11 Release readiness — verdict

**PARTIAL — not releasable yet, with no known blocking *design* defect.**

| Criterion | Status |
|---|---|
| Repository state matches the intended redesign (no stray edits, no lost features) | **PASS** |
| All five existing gates + localization parity, re-run at this commit | **PASS** (103 / 105 / 88 / 349 bindings / 375 checks, exit 0 each) |
| Negative tests prove the gates still detect defects | **PASS** (17 / 17 injected, tree restored) |
| Each previously reported bug traced to source, state change and navigation result | **PASS** (7 / 7) |
| Defects found in this pass fixed, with the tree green afterwards | **PASS** (9 defects: D1–D9, including the D9 login blocker) |
| Android build | **BLOCKED** — no SDK/JDK/Android SDK and no egress to obtain them (§9.6) |
| Runtime, navigation, forms, TalkBack, RTL rendering, screenshots | **BLOCKED** — no emulator or device (§9.7) |
| Release configuration can reach a real backend | **BLOCKED** — `ApiConfig.BaseUrl` (Release) is an intentional `.invalid` placeholder; a release APK would have no API to talk to (R8) |
| Small-screen and large-font behaviour | **PARTIAL** — designed for it, never measured |
| Performance on a low-end device | **PARTIAL** — virtualised feed, no raster assets, never profiled |

What that means in plain terms: the code is in the best statically verifiable state this
environment allows — the branch is clean and forward-only, the gates are green *and* proven to
fail when a defect is injected, and every bug the previous passes reported has been traced
through its real code path. It is **not** evidence that the app builds or runs. The
single next step that would change this verdict is executing §9.6 and §9.7 on a machine with
the .NET 10 SDK and an Android device; a Release candidate additionally needs the real API base
URL (R8). Until then this report must not be read as a release approval.
