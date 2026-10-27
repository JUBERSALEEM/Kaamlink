# KaamLink UI Redesign — Phase 0: Repository & Screen Audit

**Audited commit:** `de54334d9c2b936f643f40159c41acda0af749a6` (branch `arena/01a02fc9-kaamlink`)
**Work branch:** `arena/44c99984-kaamlink` (session branch; branched from the audited commit)
**Audit date:** 2026-10-25
**Working tree at audit start:** clean (`git status` → *nothing to commit, working tree clean*)
**Auditor method:** direct source inspection (no assumptions from phase reports or prompts)

---

## 1. What the repository actually is

| Item | Verified fact |
|---|---|
| Client project | `KaamLink.csproj` at repo root, `TargetFrameworks=net10.0-android`, `UseMaui=true`, `MauiXamlInflator=XamlC` |
| Minimum Android | `SupportedOSPlatformVersion=21.0` (Android 5.0) |
| App id / version | `com.kaamlink.app`, `0.1` / `1` |
| MVVM | `CommunityToolkit.Mvvm 8.4.0` (`[ObservableProperty]`, `[RelayCommand]`) |
| API client | `Core/Api/ApiClient.cs` + `AuthHeaderHandler` (bearer + rotating refresh), source-generated `System.Text.Json` context in `ApiContracts.cs` |
| Navigation | Shell with **15 top-level `ShellContent` routes** in `AppShell.xaml`; **all** navigation is absolute (`//route`) via `AppRoutes` |
| DI | `MauiProgram.RegisterServices` — singleton session/API, transient pages + VMs |
| Strings | `AppStrings.resx` (en, neutral) + `AppStrings.ar.resx` (ar satellite) + **hand-maintained** `AppStrings.Designer.cs` (212 keys) |
| Fonts | `Resources/Fonts/OpenSans-Regular.ttf`, `OpenSans-Semibold.ttf` only |
| Images | **none** (`Resources/Images/` does not exist; `MauiImage Include="Resources\Images\*"` resolves to nothing) |
| Styles | `Resources/Styles/Colors.xaml` (tokens) + `Resources/Styles/Styles.xaml` (base + typography + `Card`) |
| Converters | `InverseBoolConverter`, `VerificationStateColorConverter`, `StatusColorConverter`, `SelectedOptionBackgroundConverter`, `SelectedOptionTextColorConverter` (registered in `App.xaml`) |
| Backend | ASP.NET Core 10 modular monolith + PostgreSQL; auth/worker/company/vacancy/application/assessment controllers present |
| Static verifiers | `tools/verify-phase03.py` (105 checks), `tools/verify-phase04.py` (105), `tools/verify-phase05.py` (88), plus the redesign gates `tools/verify-bindings.py` (403 compiled bindings), `tools/verify-ui-redesign.py` (499 checks), `tools/verify-compile-surface.py` (416), `tools/verify-connectivity.py` (49) and `tools/verify-email-auth.py` (119) |
| Tests | xUnit unit + integration tests under `backend/tests/` |
| Sample-code leftovers | none found — every XAML page is live |

### 1.1 Active production screens (compiled into the app)

All 15 XAML pages are referenced by `AppShell.xaml` through `DataTemplate` and resolved through DI, therefore **all 15 are active production surfaces**. There are no test-only, example, or orphaned XAML files.

| # | Route (`AppShell`) | Page (XAML) | ViewModel | Role |
|---|---|---|---|---|
| 1 | `welcome` | `Features/Onboarding/RoleSelectionPage.xaml` | `RoleSelectionViewModel` | Shared |
| 2 | `login` | `Features/Auth/LoginPage.xaml` | `LoginViewModel` | Shared |
| 3 | `otp` | `Features/Auth/OtpPage.xaml` | `OtpViewModel` | Shared |
| 4 | `worker-onboarding` | `Features/Worker/WorkerOnboardingPage.xaml` | `WorkerOnboardingViewModel` | Worker |
| 5 | `workerhome` | `Features/Worker/WorkerHomePage.xaml` | `WorkerHomeViewModel` | Worker |
| 6 | `employer-onboarding` | `Features/Employer/EmployerOnboardingPage.xaml` | `EmployerOnboardingViewModel` | Employer |
| 7 | `employerhome` | `Features/Employer/EmployerHomePage.xaml` | `EmployerHomeViewModel` | Employer |
| 8 | `vacancyfeed` | `Features/Jobs/VacancyFeedPage.xaml` | `VacancyFeedViewModel` | Worker |
| 9 | `vacancydetail` | `Features/Jobs/VacancyDetailPage.xaml` | `VacancyDetailViewModel` | Worker |
| 10 | `myapplications` | `Features/Jobs/MyApplicationsPage.xaml` | `MyApplicationsViewModel` | Worker |
| 11 | `companyvacancies` | `Features/Jobs/CompanyVacanciesPage.xaml` | `CompanyVacanciesViewModel` | Employer |
| 12 | `vacancyedit` | `Features/Jobs/VacancyEditPage.xaml` | `VacancyEditViewModel` | Employer |
| 13 | `vacancyapplications` | `Features/Jobs/VacancyApplicationsPage.xaml` | `VacancyApplicationsViewModel` | Employer |
| 14 | `myassessments` | `Features/Assessments/MyAssessmentsPage.xaml` | `MyAssessmentsViewModel` | Worker |
| 15 | `assessmentrun` | `Features/Assessments/AssessmentRunPage.xaml` | `AssessmentRunViewModel` | Worker |

### 1.2 Screens the prompt anticipates that do **not** exist

Verified absent — the redesign must **not** invent them (that would fake features):

* No splash page object (only a `MauiSplashScreen` asset `Resources/Splash/splash.svg`).
* No separate "company verification status" page — verification state is rendered on `employerhome` and `vacancydetail`.
* No worker "profile edit" page after onboarding (`PUT /workers/me` exists in the API but has no screen).
* No candidate detail page — candidate data is a card row inside `vacancyapplications`.
* No notifications, profile/settings, or language-switch screen.
* No dark theme (explicitly deferred in `Colors.xaml` comments).
* No bottom tab bar / flyout (`FlyoutBehavior="Disabled"`).

**Consequence for scope:** "redesign every active screen" = the 15 pages above. Adding new screens is out of scope for a visual redesign and would be a feature change.

---

## 2. Screen inventory (12 required attributes per screen)

Legend for state columns: **L** = loading, **E** = empty, **X** = error, **S** = success/loaded.

### 1. Role selection — `Features/Onboarding/RoleSelectionPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Shared, post-auth account-type choice |
| Reached by | Successful OTP without a preselected role (`ShellContent` route `welcome`); role switch |
| Purpose | Choose the Worker or Employer experience after authentication |
| Controls | Brand row/theme toggle, value proposition, separate Worker and Employer cards, trust strip and quiet legal footer |
| VM / commands | `RoleSelectionViewModel.NavigateToWorkerCommand`, `NavigateToEmployerCommand`, `EnsureSessionRestoredAsync()` from code-behind |
| API | None directly; session identity is supplied by the successful auth response; returning session restore is handled at login |
| States | Existing cards route to onboarding/home according to the server identity; restore indicator remains available for unauthenticated role-switch entry |
| Localization | All visible strings via `x:Static AppStrings` (EN/AR present) |
| Visual problems | No app-start role prompt now; this page remains the post-login choice when no role was preselected |
| Required redesign | Implemented in the existing claymorphism role-selection screen; do not duplicate auth on this page |
| Verification | XAML, bindings, role routes, strings, accessibility and theme-resource gates pass; runtime appearance is not device-verified |

### 2. Phone sign-in — `Features/Auth/LoginPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Shared |
| Reached by | App cold start (`ShellContent` route `login`); session expiry; role switch when signed out |
| Purpose | Google option (currently unavailable until OAuth is configured) or validate UAE mobile locally and request OTP; role choice follows verification |
| Controls | Brand mark/wordmark, welcome message, separate SVG hero asset, unavailable Google button with status explanation, +971 phone field, OTP CTA, consent note |
| VM / commands | `LoginViewModel.EnsureSessionRestoredAsync()`, `Phone`, `IsBusy`, `IsRestoringSession`, `HasError`, `SendCodeCommand` |
| API | `POST /auth/otp/request`; Google OAuth is not present in the backend |
| States | L: session/request activity indicators; X: localized error banner; S: navigate to OTP and then role choice when none was preselected |
| Localization | EN/AR resx parity; Google limitation and consent copy are localized |
| Visual problems | Google sign-in is intentionally disabled until OAuth client configuration and server-side token verification exist; runtime rendering has not been observed |
| Required redesign | Implemented: app-start welcome/login, standalone mark and hero SVG assets, +971 field, functional OTP CTA, accessible validation, consent and explicit Google setup note |
| Verification | Bindings, StaticResource keys, XAML/resx parity, app-start route, role-through-OTP contract and static design gates pass; no runtime/build claim |

### 3. OTP verification — `Features/Auth/OtpPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Shared |
| Reached by | Successful OTP request |
| Purpose | Verify 6-digit code; resend with cooldown |
| Controls | Centered card, 24sp centered `Entry`, verify button, transparent resend button bound to `ResendText`/`CanResend` |
| VM / commands | `Phone`, `Code`, `SecondsRemaining`, `ResendText`, `CanResend`, `VerifyCommand`, `ResendCommand` |
| API | `POST /auth/otp/verify`, resend via `/auth/otp/request` |
| States | L: spinner; X: 12sp red line; S: session adopted → role destination |
| Localization | Complete (`ResendWaitFormat`) |
| Visual problems | Code entry is a plain text box (no cell affordance), phone number shown in a semibold body label without context ("sent to"), no "wrong number?" affordance, resend button styled like a link with a 52dp-tall default button style (huge blue-tinted rectangle area), error styling weak |
| Required redesign | Code-focused layout: masked phone line, large centered numeric field with letter-spacing, timer chip, resend action as text button with countdown, error banner component |
| Verification | Same as above + resend cooldown bindings preserved |

### 4. Worker onboarding (3 steps) — `Features/Worker/WorkerOnboardingPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Worker |
| Reached by | `RoleNavigator.RouteFor` when `Worker.OnboardingComplete == false`; from `workerhome` if profile disappears |
| Purpose | Basics → skills → preferences, then create profile |
| Controls | Step label, three `IsVisible`-switched stacks of raw `Picker`/`Entry`/`Editor` pairs, add-skill button, added-skill `BindableLayout` rows with red "Remove" buttons, footer Back/Continue/Finish |
| VM / commands | `Step`, `IsStep1..3`, `StepTitle`, catalog collections, `AddSkillCommand`, `RemoveSkillCommand`, `BackCommand`, `ContinueFromBasicsCommand`, `ContinueFromSkillsCommand`, `FinishCommand`, `LoadCommand`, `IsLoadingCatalog`, `HasError`, `IsBusy` |
| API | `GET /skills/catalog` (+ `POST/PUT /workers/me`, `/workers/me/skills`) |
| States | L: spinner; X: red text + Retry button that is visible on every step (`LoadCommand` always visible); E: empty added-skills list has no explanation; S: navigate home |
| Localization | All labels localized; **skill picker labels come localized from the API** (correct) |
| Visual problems | No visual progress indicator (just "Step 1 of 3"), fields have no grouping, `Picker` look is inconsistent across Android versions, "Remove" as a filled-looking text button in a card is noisy, error is unbounded, footer is a plain `Grid` with 24dp padding, no required-field marking, no helper text for salary |
| Required redesign | Stepper rail (3 dots + labels), sectioned form cards with field labels + hints, chip/pill list for added skills with icon-only remove, sticky footer with clear secondary/primary hierarchy, field-level validation styling |
| Verification | Step visibility logic untouched; all commands preserved; catalog loading preserved |

### 5. Worker home / Skill Passport — `Features/Worker/WorkerHomePage.xaml`

| Attribute | Finding |
|---|---|
| Role | Worker |
| Reached by | Post-login (onboarded), role switch, assessments done |
| Purpose | Live Skill Passport + entry points to jobs/applications/assessments |
| Controls | Title1 name, headline, completeness card + `ProgressBar`, skills `BindableLayout` rows (star prefix for primary), availability/salary labels, about, three stacked full-width buttons (Browse jobs, My applications, assessments), **roadmap note card**, Switch/Logout text buttons |
| VM / commands | `IsBusy/IsLoaded/HasError/ErrorMessage`, `DisplayName`, `Headline`, `CompletenessText/Progress`, `AvailabilityText`, `SalaryText/HasSalary`, `About/HasAbout`, `Skills`, `LoadCommand`, `BrowseJobsCommand`, `OpenMyApplicationsCommand`, `OpenMyAssessmentsCommand`, `SwitchToEmployerCommand`, `LogoutCommand` |
| API | `GET /workers/me` |
| States | L: top spinner; X: red text + Retry; S: content. Empty skills → empty section with no message |
| Localization | Complete; **skill state badge is always "Self-declared"** (honest, matches backend: assessments run elsewhere) |
| Visual problems | Five stacked full-width buttons with identical weight = no hierarchy; the "roadmap note" card contradicts the shipped job/assessment features (stale copy); star glyph `★` as icon; no avatar/initials; no section grouping; completeness card is a big empty white box with one line; profile metadata is unstructured text lines |
| Required redesign | Profile header (initials avatar, name, profession · emirate, completeness ring/bar + next-step hint), "quick actions" 3-up tile row, Skill Passport as sectioned list with years + state pill, availability & salary as a definition-list card, About block, account section at bottom with quiet destructive logout |
| Verification | All bindings/commands preserved; `Skills` template `x:DataType` intact |

### 6. Employer home — `Features/Employer/EmployerHomePage.xaml`

| Attribute | Finding |
|---|---|
| Role | Employer |
| Reached by | Post-login when memberships exist; role switch |
| Purpose | Company cards with real verification state + entry to vacancy management |
| Controls | Section label, company `BindableLayout` cards (name + verification outline pill + category/emirate + membership role + pending note + rejection reason + "Manage vacancies" primary button), stale roadmap note, switch/logout |
| VM / commands | `Companies`, `IsBusy/IsLoaded/HasError/ErrorMessage`, `LoadCommand`, `ManageVacanciesCommand`, `SwitchToWorkerCommand`, `LogoutCommand` |
| API | `GET /companies/mine` (via `GetMyCompaniesAsync`) |
| States | L/X like worker home; **empty → auto-navigate to employer onboarding** (no empty state needed, but there is a flash) |
| Localization | Complete |
| Visual problems | Verification is the product's trust anchor yet rendered as a thin outline pill in the corner; stale "posting not available" note; card contains a full-width primary button per company (many companies = wall of blue); no company avatar/initial; no verification explainer |
| Required redesign | Company identity header with initial tile + verification banner (pending = amber note with what happens next; rejected = danger banner with reason; verified = trust mark), membership role chip, primary "Manage vacancies" only when verified/pending per server rules (unchanged behavior), account footer |
| Verification | `VerificationState` string still drives tone; commands preserved; conditional pending/reason visibility preserved |

### 7. Employer onboarding — `Features/Employer/EmployerOnboardingPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Employer |
| Reached by | Login when no membership; employer home fallback |
| Purpose | Register company → Pending review |
| Controls | Title2 + subtitle, five label/field pairs, hint, submit button, spinner |
| VM / commands | `CompanyName`, `SelectedCategory`, `SelectedEmirate`, `Area`, `TradeLicenseNumber`, `IsBusy`, `HasError`, `ErrorMessage`, `SubmitCommand` |
| API | `POST /companies` then `/workers/me`-style reload via `ReloadUserAsync` |
| States | L: spinner under button (double feedback), X: red text at top, S: navigate to employer home |
| Localization | Complete |
| Visual problems | Everything is a flat label→input stack (no grouping), no visual "review process" explainer beyond hint text, no required markers, submit is far below the fold with no sticky action, no success/confirmation of pending status before leaving |
| Required redesign | Intro card explaining pending review, two form sections (business identity / location & licence), required markers, sticky primary "Submit for review", helper text for trade licence format |
| Verification | Validation rules untouched (VM-level); commands preserved |

### 8. Vacancy feed — `Features/Jobs/VacancyFeedPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Worker |
| Reached by | Worker home CTA |
| Purpose | Browse/search active vacancies with emirate filter, paging |
| Controls | Search `Entry` + Search button, emirate `Picker`, spinner, error block, empty label, vacancy `BindableLayout` cards with tap gesture, Load more |
| VM / commands | `SearchText`, `SelectedEmirate`, `EmirateOptions`, `Items`, `IsBusy/IsLoaded/HasError/IsEmpty/HasMore`, `SearchCommand`, `LoadMoreCommand`, `OpenVacancyCommand` |
| API | `GET /vacancies?emirate=&q=&page=&pageSize=` |
| States | L: spinner above list (existing rows stay); X: red text + Retry; E: one line of secondary text (no action); S: cards; paging footer |
| Localization | Complete; server-side skill names localized via `LocalizationHelpers` |
| Visual problems | Search "button" is a full-width blue button next to the field (48dp+ of blue), `Picker` as filter has no "All emirates" affordance, cards are 20dp-padded white blocks with 6 text lines and no visual scanning anchors (no salary emphasis, no company identity), **verified company shown as a literal "✓" appended to the name string**, empty state is a bare sentence, list inside `ScrollView`+`BindableLayout` renders every page item (unbounded view creation) |
| Required redesign | Sticky search bar with leading search icon and inline clear, horizontal filter chips (All + emirates) — *must keep the same `SelectedEmirate` binding*, result count line, vacancy cards with company initial, title, location w/ pin icon, salary emphasis, skill chips, verified badge as a component; `CollectionView` with header/empty view/footer for virtualisation |
| Verification | `SelectedEmirate` type remains `EnumOption`; `SearchCommand`/`LoadMoreCommand`/`OpenVacancyCommand` preserved; `IsEmpty`/`HasMore` behavior preserved |

### 9. Vacancy detail + apply — `Features/Jobs/VacancyDetailPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Worker |
| Reached by | Feed card tap → `//vacancydetail?vacancyId=` |
| Purpose | Read vacancy, apply with optional cover note, see own application status |
| Controls | Title1 (**page Title is misleadingly "Jobs"**), company + verification pill, location/salary/skills/headcount lines, description, own-application pill, apply section (`Editor` + Apply) |
| VM / commands | `Title`, `CompanyLine`, `CompanyVerificationState/Label`, `LocationLine`, `SalaryText`, `SkillsLine`, `HeadcountText`, `Description/HasDescription`, `OwnApplicationStatus/Label/HasOwnApplication`, `CanApply`, `CoverNote`, `LoadCommand`, `ApplyCommand` |
| API | `GET /vacancies/{id}` (includes `OwnApplicationStatus`), `POST /vacancies/{id}/applications` |
| States | L/X/S as above; **no applied-success feedback** beyond the pill appearing |
| Localization | Complete |
| Visual problems | Page title wrong; no back-context header; salary/skills/headcount as three unlabelled text lines of equal weight; description has no heading; apply `Editor` unlabelled; no warning that one application per vacancy is enforced; after applying, the CTA vanishes with no confirmation |
| Required redesign | Header block (title, company with verified mark, location/salary meta grid), "Job details" section with labelled rows + skill chips, "About this role" description card, application status banner (states from server), apply panel with labelled note field and primary CTA + honest one-application note; page Title bound to vacancy title |
| Verification | `CanApply`/`HasOwnApplication` logic untouched; commands preserved |

### 10. My applications — `Features/Jobs/MyApplicationsPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Worker |
| Reached by | Worker home CTA |
| Purpose | Track applications and withdraw while open |
| Controls | Spinner, error, empty label, `BindableLayout` cards (title + status pill, company + date, withdraw text button) |
| VM / commands | `Items` (`ApplicationRowModel`), `IsBusy/HasError/IsEmpty`, `LoadCommand`, `WithdrawCommand` |
| API | `GET /workers/me/applications`, `POST /applications/{id}/withdraw` |
| States | L/X/E/S; no per-row busy state |
| Localization | Complete |
| Visual problems | Cards have no visual timeline/step affordance; status pill equal weight to title; withdraw is a large red text button with default 52dp height inside the card (dominant); no date grouping/sorting cue |
| Required redesign | Status-grouped feel: card with title, company, date, status pill top-right, and a quiet destructive "Withdraw" action row; empty state with CTA back to feed (no new command → the page has none; use static text) |
| Verification | `CanWithdraw` still gates the action; commands preserved |

### 11. Employer vacancy list — `Features/Jobs/CompanyVacanciesPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Employer |
| Reached by | Employer home → Manage vacancies (`?companyId=&companyName=`) |
| Purpose | List company vacancies, create/edit, activate/pause/close, open applicants |
| Controls | Title1 company name + "New vacancy" primary button in a 2-col grid, spinner, error, empty label, `BindableLayout` cards (title + status pill, meta line, applications-count button, activate/pause/close buttons) |
| VM / commands | `CompanyName`, `Items`, `LoadCommand`, `NewVacancyCommand`, `OpenApplicationsCommand`, `ActivateCommand`, `PauseCommand`, `CloseCommand` |
| API | `GET /companies/{id}/vacancies`, `POST /vacancies/{id}/status` |
| States | L/X/E/S; no optimistic row update or per-row busy |
| Localization | Complete |
| Visual problems | Company name in 30sp Title1 next to a full-size button (overflow on 360dp with long Arabic names); three action buttons per card with equal weight; applications count as a borderless "button" that looks like a link with default 52dp height; no draft/active visual distinction |
| Required redesign | Compact header with company name (H1, wrapping) + inline "+ New" action, vacancy cards with status pill, key meta, applicant count as a clear tappable row with icon, and a compact secondary action row (Activate/Pause/Close) with appropriate tones |
| Verification | All four row commands preserved; `CanActivate/CanPause/CanClose` preserved |

### 12. Vacancy editor — `Features/Jobs/VacancyEditPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Employer |
| Reached by | Company vacancies → New/Edit (route accepts `companyId`, optional `vacancyId`) |
| Purpose | Create or edit a vacancy (draft/active per server rules) |
| Controls | Caption labels + `Entry`/`Editor`/`Picker`, salary 2-col, headcount, **skills `CollectionView` in a bordered box with `SelectionMode="Multiple"` and 220dp fixed height**, Save |
| VM / commands | `Title`, `Description`, `SelectedEmirate`, `Area`, `SalaryMinText/MaxText`, `HeadcountText`, `SkillOptions`, `SelectedSkills`, `IsBusy/IsLoaded/HasError`, `SaveCommand`, `PageTitle` |
| API | `GET /skills/catalog`, `PUT/POST vacancies` |
| States | L/X/S; selection feedback relies on Android's default (subtle) |
| Localization | Complete |
| Visual problems | Fixed 220dp scroll box inside a page that also scrolls (nested scroll), no selection count, no way to see what's selected after scrolling, Save is far below, labels are 12sp captions above 15sp fields with no grouping, no field-level validation |
| Required redesign | Sectioned form, labelled fields, skills as a multi-select sheet-like bordered list **with a selected-count line and searchable? no search exists** → keep list but add count + clearer selection styling (selectable rows via `CollectionView` item template with a check glyph bound to selection is not directly bindable → use `SelectionMode=Multiple` + a `Label` reflecting `SelectedSkills.Count`? Binding to `SelectedSkills.Count` from VM works (`SelectedSkills` is `ObservableCollection<object>`), keep VM unchanged; add count text via existing collection's binding? Careful: `SelectedSkills.Count` binding updates only if the collection raises change notifications — `ObservableCollection` does raise `CollectionChanged`, but `Count` binding updates require `INotifyPropertyChanged` on the binding path; MAUI's binding to `Count` of ObservableCollection does update. Risky. Alternative: don't show a live count; instead keep static hint "Select all that apply". Decision: static hint text, no new binding) |
| Verification | `SelectedSkills` multi-select binding preserved; commands preserved; no VM change |

### 13. Applicants / decisions — `Features/Jobs/VacancyApplicationsPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Employer |
| Reached by | Vacancy list → applications count (`?companyId=&vacancyId=&title=`) |
| Purpose | Review applicants and decide (viewed/shortlist/hire/reject) |
| Controls | Title1 vacancy title, spinner, error, empty label, `BindableLayout` applicant cards: name + status pill, phone, meta, cover note, four action buttons in a `HorizontalStackLayout` |
| VM / commands | `VacancyTitle`, `Items` (`ApplicantRowModel`), `LoadCommand`, `MarkViewedCommand`, `ShortlistCommand`, `HireCommand`, `RejectCommand` |
| API | `GET /vacancies/{id}/applications`, `POST .../status` |
| States | L/X/E/S; no busy state per action |
| Localization | Complete |
| Visual problems | Four buttons in one horizontal row (labels like "Shortlist" and "Hire" plus two text buttons) — **overflow/truncation on 360dp and in Arabic**, competing primary weights, no candidate identity anchors (no initials), cover note raw text, phone number unformatted emphasis |
| Required redesign | Candidate card: initials avatar + name + status pill, meta line (skills/experience/emirate, from `MetaLine`), cover note in a quote block, phone as a secondary row with icon, decisions as a **2×2 or wrapped action grid** with clear tones (Shortlist primary, Hire secondary/positive, Reject destructive-text, Mark viewed text) |
| Verification | All four commands and `CanDecide`/`CanMarkViewed` gating preserved; no horizontal overflow at 360dp / long Arabic |

### 14. My assessments — `Features/Assessments/MyAssessmentsPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Worker |
| Reached by | Worker home CTA |
| Purpose | Claimed skills + assessment availability/state/score, start attempt |
| Controls | `Grid` with spinner row, error stack, empty stack, `CollectionView` of assessment cards |
| VM / commands | `Items` (`AssessmentRowModel`), `IsBusy/IsEmpty/IsLoaded/HasError`, `LoadCommand`, `OpenAssessmentCommand` |
| API | `GET /workers/me/assessments`, then `POST .../attempts` |
| States | L/X/E/S; "not available" caption |
| Localization | Complete (`StateSelfDeclared`, `StateAssessmentVerified`, score format) |
| Visual problems | 20dp page padding differs from other screens (24); state pill colors come from `StateRaw`; no explanation of why an assessment is unavailable; no score visual (just text); no "what verification means" trust copy |
| Required redesign | Intro explaining verified skills + server scoring, list rows with skill name, state pill, score/percentage emphasis and Start CTA; unavailable rows explain honestly |
| Verification | Existing converters/keys reused (`verify-phase05.py` requires converter usage in the runner, not here); commands preserved |

### 15. Assessment runner — `Features/Assessments/AssessmentRunPage.xaml`

| Attribute | Finding |
|---|---|
| Role | Worker |
| Reached by | My assessments → Start (`?skillId=&skillName=`) |
| Purpose | Answer one MCQ attempt, submit, see server verdict |
| Controls | Spinner, error+retry, pass-mark caption, `CollectionView` of question cards with option `Button`s bound to selection converters, answered count, submit, result card + Done |
| VM / commands | `SkillName`, `Questions`, `PassMarkText`, `AnsweredCountText`, `HasResult`, `ResultTitle/ScoreText/Note/State`, `StartCommand`, `SelectOptionCommand`, `SubmitCommand`, `DoneCommand` |
| API | `POST /workers/me/assessments/{skillId}/attempts`, `POST .../attempts/{id}/submit` |
| States | L/X/S + result pass/fail |
| Localization | Complete |
| Visual problems | Options are full-width filled buttons (selected = brand fill) that look like CTAs, no A/B/C marker, no progress indication across questions, no confirmation before submitting an incomplete attempt (server enforces completeness via `CanSubmitAll`), result panel is a plain card |
| Required redesign | Assessment header (skill, pass mark chip, answered progress bar), question cards numbered, options as selectable rows with radio indicator (achieved with existing `IsSelected` + converters, styled as row: border + circle glyph + label), sticky submit with answered count; verdict panel with pass/fail icon, big score, note, Done |
| Verification | `verify-phase05.py` requires `SelectedOptionBackground`/`SelectedOptionText` in this page → keep both keys wired (may be on the selection indicator + row background); commands preserved |

---

## 3. Design-system findings

### 3.1 What exists (and is decent)

* Semantic color tokens in `Colors.xaml` (brand, semantic, gray ramp, surfaces, text, brushes).
* Typography styles `Title1/Title2/SectionTitle/Body/BodySecondary/Caption` + implicit `Label`, `Button`, `Entry`, `Picker`, `Editor`, `ActivityIndicator`, `ContentPage` styles.
* One `Card` style (20dp padding, 20 radius, shadow `Opacity 0.07`, `Radius 18`).
* Converters mapping API enum names → token colors (good: semantic, not hard-coded per page).
* Every page already uses `x:DataType` compiled bindings, `SemanticProperties`, and `x:Static` strings.

### 3.2 Problems (evidence-based)

| ID | Severity | Finding |
|---|---|---|
| D1 | High | **Iconography is emoji** (`👷`, `🏭`) and a `★` text prefix; no icon font or vector assets exist in the project |
| D2 | High | **Every card is identical**: white, 20dp radius, 20dp padding, shadow. Information density is low; a 360dp screen fits ~2 cards |
| D3 | High | **No reusable components** — status pills (Border+Label) are copy-pasted in 6 pages with slightly different padding (10,3 vs 8,2 vs 12,10), error blocks in 9 pages, spinners in 9 pages |
| D4 | High | **No hierarchy for actions**: primary filled buttons are used for navigation, secondary commands and destructive actions; several pages have 3–5 full-width blue buttons |
| D5 | Medium | Spacing is inconsistent across pages: page padding `24` (most), `20` (assessments), `16`(none), card padding `20` (style) vs `16` (inline) vs `12,10`; no spacing tokens |
| D6 | Medium | Typography is under-specified for operational text: no overline/label style, captions at 12sp used for error messages and validation; no line-height control |
| D7 | Medium | Navigation chrome unstyled: default Shell nav bar colors, page `Title` often wrong (`VacancyDetailPage` says "Jobs") or missing context (`MyAssessments` says "Assessments") |
| D8 | Medium | **States are text-only**: empty states are one secondary sentence; errors are a red 12–14sp line; loading is an unbounded spinner that never reserves space |
| D9 | Medium | Stale/incorrect product copy shipped: worker and employer homes still say jobs/assessments are "not in this build" (they shipped in Phase 04/05) |
| D10 | Medium | RTL not verified: no `FlowDirection` handling, several `HorizontalStackLayout` action rows and fixed 2-column grids will crowd or clip in Arabic; no Arabic-capable font family in `Resources/Fonts` (OpenSans has no Arabic glyphs → relies on Android fallback) |
| D11 | Low | Redundant loading feedback (spinner in content *and* one under the button) on login/onboarding |
| D12 | Low | Lists use `ScrollView`+`BindableLayout` (no virtualisation) on the feed; nested scroll region in the vacancy editor |
| D13 | Low | Brand inconsistency: `Colors.xaml` says indigo `#4F46E5`, Android native `colors.xml` says teal `#0F766E`, splash/icon use `#4F46E5` |
| D14 | Low | No elevation/shadow restraint: card shadow is on every card including long lists (Android over-draw + low-end GPU cost) |
| D15 | Low | Touch targets: inline pill buttons (`Padding 10,3`, label 12sp) and link buttons are below 48dp; some text buttons inherit 52dp height inside cards |

### 3.3 Constraints the redesign must respect (discovered, non-negotiable)

1. **Verifier contract** (`tools/verify-phase03.py`): every `.xaml` must be well-formed XML; EN/AR resx key sets must be identical; `AppStrings.Designer.cs` properties must equal the resx key set exactly; every `x:Static strings:AppStrings.X` must exist; **every `{StaticResource key}` must be declared in `Colors.xaml`, `Styles.xaml` or `App.xaml`** (other files are not scanned) → new tokens/components must be declared in those three files or the verifier must be extended deliberately.
2. `verify-phase04.py` requires the string `StatusColorConverter` to appear **≥4 times across the four job pages**.
3. `verify-phase05.py` requires `SelectedOptionBackground` and `SelectedOptionText` to appear in `AssessmentRunPage.xaml` and be registered in `App.xaml`.
4. Routes (`AppShell.xaml`) must equal `AppRoutes` constants; no `Routing.RegisterRoute`.
5. All DI page/VM registrations must remain; no service locator.
6. `MauiProgram` has no `ConfigureFonts` entry to remove; any new font must be added there.
7. `KaamLink.csproj` must keep `Compile Remove="backend\**\*.cs"`.
8. `SupportedOSPlatformVersion=21.0` must not be raised.
9. `MauiXamlInflator=XamlC` — XAML must be valid for compiled XAML; **risky/exotic markup should be avoided** because the sandbox has **no .NET SDK** (see §5).
10. All user-visible text must come from `AppStrings` (`x:Static` or `Binding` from a VM-localized string).

---

## 4. Risk register

| # | Risk | Mitigation |
|---|---|---|
| R1 | No .NET SDK / Android SDK / JDK in the environment → no build, no emulator, no runtime screenshots | Deliver static verification (XAML/XML parse, resource resolution, binding-vs-VM cross-check, localization parity, RTL/textual checks), plus a purpose-built verifier; report build as **BLOCKED** with the exact reason; keep XAML conservative |
| R2 | Compiled-binding regressions from restructuring templates | Restrict bindings to members verified in VM source; run a binding cross-check script over every page; never rename VM members |
| R3 | `x:Reference` command bindings inside `DataTemplate` break if the page element name changes | Keep `x:Name="Page"` where already used; keep `Source={x:Reference Page}` pattern |
| R4 | Component (`ContentView` + `BindableProperty`) errors cannot be caught without a build | Keep components minimal (3), use only documented `BindableProperty.Create` signatures and `x:Reference` self-binding |
| R5 | Arabic text expansion clipping fixed 2-column grids / horizontal action rows | Use wrapping layouts (`FlexLayout`/stacked rows), avoid fixed heights on text, verify with long Arabic strings statically |
| R6 | Icon font not registered or codepoint wrong → tofu boxes | Register `lucide.ttf` as `Lucide` in `MauiProgram`; codepoints extracted from the font's own `info.json` and **verified against the TTF cmap**; a verifier check asserts every icon codepoint used in XAML exists in the font |
| R7 | New resx keys drift between EN/AR/Designer | Add keys to all three files in the same edit; verifier enforces equality |
| R8 | Changing `Card`/`Button` implicit styles could break non-redesigned surfaces | Redesign covers all 15 pages in the same change set; explicit styles preferred over implicit magic |
| R9 | Losing existing behavior (e.g. `LoadCommand` on every onboarding step, resend cooldown, withdraw gating) | Screen-by-screen checklist in §6 + diff review per group |

---

## 5. Environment capability check (verified)

| Capability | Result |
|---|---|
| `dotnet` CLI | **not installed** (`command not found`) — no SDK, no workload, no MSBuild |
| JDK (`java`) | **not installed** |
| Android SDK / emulator | **absent** ($ANDROID_HOME empty, no `~/Android`) |
| Network egress | only `github.com`, `codeload.github.com`, `api.github.com`, `registry.npmjs.org`, `pypi.org`, `files.pythonhosted.org` → cannot fetch the .NET SDK installer (`dot.net`) or Android command-line tools (`dl.google.com`) |
| Python 3 | available (3.11) |
| Static verifiers | runnable |
| Rendering mockups | Python + Pillow (installed into an isolated venv) + repo fonts + Lucide icon font + Cairo (Arabic) from npm |

**Consequence:** a real Android build is **BLOCKED** in this environment. All implementation must therefore be verified by an expanded static verifier suite (see `03-implementation-and-verification-report.md`), and the report will state that no build/runtime claim is being made.

---

## 6. Implementation order & completion checklist

Groups are ordered so the foundation lands before screens, and so shared components stabilise early.

| Group | Scope | Status |
|---|---|---|
| 0 | Audit (this document) | ✅ |
| 1 | Mockups (16+ screens, EN + AR specimens) + catalog + design-system spec | ✅ |
| 2 | Design system implementation: colors, typography, spacing, buttons/fields/pills/cards, icon font, Shell chrome, components (`IconTile`, `StatusPill`, `EmptyState`, `ErrorBanner`, `SectionHeader`) | ✅ |
| 3 | Entry & auth: role selection, login, OTP | ✅ |
| 4 | Worker onboarding + Skill Passport home | ✅ |
| 5 | Job discovery: feed, detail/apply, my applications | ✅ |
| 6 | Assessments: list, runner, result | ✅ |
| 7 | Employer: onboarding, home/verification, vacancies, editor, applicants | ✅ |
| 8 | EN/AR strings for all new copy, RTL/accessibility/responsive pass | ✅ |
| 9 | Verification suite + report | ✅ |

### Per-screen completion checklist (must all be true before "PASS")

- [ ] Visual layout matches the approved mockup for that screen
- [ ] Every `{Binding}` resolves to a VM/model member that exists (script-verified)
- [ ] Every command from the pre-redesign page still reachable
- [ ] Loading state visible and reserved (no layout jump)
- [ ] Empty state has a clear message (and no fake action)
- [ ] Error state is a component with retry where a retry command exists
- [ ] Success/loaded state shows real server data only
- [ ] All visible text localized (EN + AR), no hardcoded English
- [ ] RTL-safe: only Start/End alignment, wrapping layouts, no fixed LTR-only rows
- [ ] Accessibility: heading levels, control names, ≥44dp touch targets
- [ ] 360dp width safe (no horizontal overflow)

---

## 7. Baseline design direction (chosen after reviewing the existing brand)

The repo's identity is a **generic indigo/violet SaaS palette** (`#4F46E5` primary + `#7C3AED` secondary + amber), while the Android platform chrome is **teal** (`#0F766E`) and the splash/icon are indigo — three different stories. The product's value proposition is *trustworthy verification of practical skill*, not "startup SaaS".

**Direction: "Marine Blue + Trust Teal"** — a disciplined, low-chrome identity:

* A rich, modern **aurora indigo/violet** for primary action and brand (`#4B3FD6`), with a luminous `#8B7BFF` gradient top and a darker pressed state — see the current palette in `04-claymorphism-theme.md`.
* A restrained **cyan/mint** accent (`#0B6E7A` / `#5DE3D4` light, `#6FE7D2` dusk) reserved for *verification/trust* moments (verified marks, assessment accents).
* Warm amber for pending states, calm green for success, explicit red for destructive/rejected — always paired with text, never colour alone.
* Neutral, slightly cool greys for canvas/borders/text; **flat cards with 1px borders and no drop shadows** (faster on low-end devices, calmer visually).
* Compact spacing rhythm (4/8/12/16/20/24; 16dp screen margin), 12–14dp radii, 48dp controls, 15sp body.
* Real iconography from a licensed icon font (Lucide, ISC) replacing all emoji.

Exact values, contrast ratios and component dimensions are recorded in `02-design-system-specification.md`; mockups are in `mockups/`.
