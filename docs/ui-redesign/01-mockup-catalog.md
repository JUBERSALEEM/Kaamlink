# KaamLink UI Redesign — Mockup Catalog

**Mockups:** `docs/ui-redesign/mockups/*.png` (720 × 1560 px = 360 × 780 dp @2×, Android phone proportions)
**Renderer:** `docs/ui-redesign/mockups/mockup_kit.py` + `render_mockups.py` (deterministic structured layout, real fonts → exact text)
**Regenerate:** `python3 render_mockups.py` (needs Pillow, arabic-reshaper, python-bidi — see `mockups/README.md`)

> These are **not** AI-generated images. Every screen is laid out from a structured
> description and drawn with the app's own typeface (Open Sans), the Lucide icon
> font, and Noto Sans Arabic for the Arabic specimens, so the interface text in the
> PNGs is exact and the geometry is measurable. This was a deliberate choice: image
> generators produce malformed interface text, which is useless as an implementation
> reference.

Mockup data is fictional and plausible (UAE trades, AED salaries). No real personal
data is used. Phone numbers are in the reserved-looking example range.

---

## 1. Screen ↔ mockup ↔ source mapping

Every mockup maps to exactly one shipped screen (or to the design system), and every
shipped screen has a mockup. "Source" is the XAML file that the mockup governs.

### A. Shared entry & authentication

| # | Mockup | Shipped screen | Source | States shown |
|---|---|---|---|---|
| 01 | `01-welcome.png` | Role selection (welcome) | `Features/Onboarding/RoleSelectionPage.xaml` | loaded, both roles, trust strip, legal line |
| 02 | `02-login.png` | Phone sign-in | `Features/Auth/LoginPage.xaml` | role context, valid input, **error** state, busy CTA |
| 03 | `03-otp.png` | OTP verification | `Features/Auth/OtpPage.xaml` | 4/6 digits entered, resend countdown, **error** state |

### B. Worker experience

| # | Mockup | Shipped screen | Source | States shown |
|---|---|---|---|---|
| 04 | `04-worker-onboarding-basics.png` | Worker onboarding · step 1 | `Features/Worker/WorkerOnboardingPage.xaml` | stepper, sectioned form, sticky continue |
| 05 | `05-worker-onboarding-skills.png` | Worker onboarding · step 2 | same | add-skill form, added skills with remove, back + continue |
| 06 | `06-worker-onboarding-preferences.png` | Worker onboarding · step 3 | same | availability, salary range, about, finish |
| 07 | `07-worker-home.png` | Worker home / Skill Passport | `Features/Worker/WorkerHomePage.xaml` | loaded, completeness, skill states, availability |
| 08 | `08-vacancy-feed.png` | Vacancy feed | `Features/Jobs/VacancyFeedPage.xaml` | search, emirate chips, 3 cards, verified/unverified employers |
| 09 | `09-vacancy-detail.png` | Vacancy detail + apply | `Features/Jobs/VacancyDetailPage.xaml` | loaded + apply panel, one-application note |
| 10 | `10-my-applications.png` | My applications | `Features/Jobs/MyApplicationsPage.xaml` | shortlisted / rejected / applied+withdraw / hired |
| 11 | `11-my-assessments.png` | My assessments | `Features/Assessments/MyAssessmentsPage.xaml` | assessed+score, available+start, not-available |
| 12 | `12-assessment-run.png` | Assessment runner | `Features/Assessments/AssessmentRunPage.xaml` | progress, answered count, selected option, submit |
| 13 | `13-assessment-result.png` | Assessment result (same page, result state) | `Features/Assessments/AssessmentRunPage.xaml` | server verdict: passed + score + passport note |

### C. Employer experience

| # | Mockup | Shipped screen | Source | States shown |
|---|---|---|---|---|
| 14 | `14-employer-onboarding.png` | Company registration | `Features/Employer/EmployerOnboardingPage.xaml` | review explainer, business/location sections, submit |
| 15 | `15-employer-home-pending.png` | Employer home | `Features/Employer/EmployerHomePage.xaml` | **Pending** verification banner + company card |
| 16 | `16-employer-home-verified.png` | Employer home | same | **Verified** banner + company card |
| 17 | `17-company-vacancies.png` | Company vacancies | `Features/Jobs/CompanyVacanciesPage.xaml` | Active / Draft / Paused rows, applicant counts, actions |
| 18 | `18-vacancy-edit.png` | Vacancy editor | `Features/Jobs/VacancyEditPage.xaml` | draft badge, sections, skills multi-select |
| 19 | `19-vacancy-applicants.png` | Applicants & decisions | `Features/Jobs/VacancyApplicationsPage.xaml` | cover note, 2×2 decision grid for 3 gating combinations |

### D. Design system & states

| # | Mockup | Purpose |
|---|---|---|
| 20 | `20-states.png` | Loading, empty, error (with retry), success, every status pill, button hierarchy incl. disabled |
| 21 | `21-design-system.png` | Color swatches + hex, type scale, spacing scale, buttons, pills, form field valid/error, card/list row, Arabic/RTL specimen |

### E. Arabic / RTL specimens

| # | Mockup | Source | Notes |
|---|---|---|---|
| 22 | `22-arabic-worker-home.png` | `WorkerHomePage.xaml` | full RTL mirror: avatar at start, progress fills right→left, pills at start, text right-aligned |
| 23 | `23-arabic-vacancy-feed.png` | `VacancyFeedPage.xaml` | RTL app bar (back arrow mirrored), search, chips, cards with Arabic company/skill names and Latin numerals |

Arabic mockups are rendered with the Noto Sans Arabic face to emulate Android's Arabic
fallback (the shipped app keeps Open Sans for Latin and lets the platform supply the
Arabic face — see the design spec §2 for the RTL rules the implementation must follow).

---

## 2. Mockup review gate (consistency pass before implementation)

Reviewed against every screen in section 1:

| Check | Result |
|---|---|
| Alignment & spacing | One 16dp screen margin and one 14dp card padding everywhere; 12dp vertical card rhythm; no page uses a different margin |
| Typography | Only the eight defined styles are used; no screen introduces a size outside the scale |
| Button hierarchy | Exactly one filled primary action per screen region; secondary = outlined; destructive = soft red; navigation actions = text style |
| Touch targets | Every button ≥ 44dp; icon-only affordances sit in 44dp hit areas; chips 32dp tall with 6dp gaps inside a 44dp scroll region (documented exception, spec §6) |
| Navigation consistency | Every non-root screen has a back affordance; root screens carry the brand row; page titles are the screen's name, not a section name |
| Component reuse | Same status pill, empty state, error note, field, card and list row appear on every screen that needs them |
| Contrast | All text/background pairs ≥ 4.5:1, status pills ≥ 4.5:1 (values recorded in the spec) |
| Arabic / RTL | Two full RTL mockups; all layouts use start/end logic; the 2×2 decision grid and the stepper mirror correctly |
| Small-screen fit | Composed at 360dp; no horizontal row exceeds the content width (the applicants' 4-in-a-row actions were replaced by a 2×2 grid precisely because 4 buttons did not fit; the old implementation overflowed) |
| Information density | Feed card: 5 information lines, 1 CTA; applicant card: identity + status + note + 2×2 decisions; no card is taller than ~45% of the viewport |
| Workflow accuracy | Every control on every mockup exists in its ViewModel (checked against the audit §2); nothing fake was introduced; the mockups show only states the API can produce |

Deliberate deviations from the old UI, all justified by real problems found in the audit:
emoji removed (D1), one card style with no drop shadow (D2/D14), copy that claimed
jobs/assessments were "not in this build" removed (D9), 4-across applicant actions
replaced with a 2×2 grid (D10/overflow), page titles corrected (D7).

---

## 3. What the mockups deliberately do **not** show

These would be new features, not a redesign, and are therefore out of scope:

* Bottom navigation / tabs (the app's navigation model is absolute Shell routes).
* Notifications, saved jobs, in-app chat, profile editing after onboarding.
* Language switch UI (language follows the device, as today).
* Dark theme (deferred in the existing palette comments; the tokens are structured so it can be added).
* Company verification admin screens (admin is backend-only in V1).
