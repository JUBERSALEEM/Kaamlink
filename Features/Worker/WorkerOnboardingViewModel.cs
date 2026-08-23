using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Worker
{
    /// <summary>
    /// Progressive worker onboarding in three steps (basics → skills → preferences).
    /// Every step persists via the API as it completes (the server is the source of
    /// truth for completeness); the flow ends only when the server says onboarding
    /// is complete — the client never declares success on its own (§50).
    /// </summary>
    public partial class WorkerOnboardingViewModel : ObservableObject
    {
        private const int TotalSteps = 3;

        private readonly IApiClient _api;
        private readonly ISessionService _session;

        private SkillCatalogResponseDto? _catalog;
        private Guid? _primarySkillId;

        public WorkerOnboardingViewModel(IApiClient api, ISessionService session)
        {
            _api = api;
            _session = session;
        }

        // ---- step state ----

        [ObservableProperty]
        private int _step = 1;

        public bool IsStep1 => Step == 1;
        public bool IsStep2 => Step == 2;
        public bool IsStep3 => Step == 3;
        public string StepTitle => string.Format(AppStrings.StepTitleFormat, Step, TotalSteps);

        partial void OnStepChanged(int value)
        {
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(StepTitle));
        }

        // ---- shared state ----

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isLoadingCatalog;

        [ObservableProperty]
        private bool _catalogReady;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _hasError;

        // ---- step 1: basics ----

        [ObservableProperty]
        private string _displayName = string.Empty;

        public ObservableCollection<SkillCategoryOption> ProfessionCategories { get; } = [];

        public ObservableCollection<SkillOption> ProfessionOptions { get; } = [];

        [ObservableProperty]
        private SkillCategoryOption? _selectedProfessionCategory;

        [ObservableProperty]
        private SkillOption? _selectedProfession;

        public IReadOnlyList<EnumOption> Emirates => EnumDisplay.Emirates;

        [ObservableProperty]
        private EnumOption? _selectedEmirate;

        [ObservableProperty]
        private string _area = string.Empty;

        // ---- step 2: skills ----

        public ObservableCollection<SkillCategoryOption> SkillCategories => ProfessionCategories;

        public ObservableCollection<SkillOption> SkillOptions { get; } = [];

        [ObservableProperty]
        private SkillCategoryOption? _selectedSkillCategory;

        [ObservableProperty]
        private SkillOption? _selectedSkill;

        [ObservableProperty]
        private string _yearsText = string.Empty;

        public ObservableCollection<AddedSkillModel> AddedSkills { get; } = [];

        // ---- step 3: preferences ----

        public IReadOnlyList<EnumOption> Availabilities => EnumDisplay.Availabilities;

        [ObservableProperty]
        private EnumOption? _selectedAvailability;

        [ObservableProperty]
        private string _salaryMinText = string.Empty;

        [ObservableProperty]
        private string _salaryMaxText = string.Empty;

        [ObservableProperty]
        private string _about = string.Empty;

        // ---- lifecycle ----

        private bool _loaded;

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (_loaded || IsLoadingCatalog)
                return;

            IsLoadingCatalog = true;
            ClearError();
            try
            {
                _catalog = await _api.GetSkillsCatalogAsync();
                ProfessionCategories.Clear();
                foreach (var category in _catalog.Categories)
                    ProfessionCategories.Add(new SkillCategoryOption(
                        category, LocalizationHelpers.Localized(category.NameEn, category.NameAr)));
                CatalogReady = true;
                _loaded = true;
            }
            catch (ApiException exception) when (SessionErrors.IsExpiredSession(exception))
            {
                await SessionErrors.RouteToWelcomeAsync();
            }
            catch (ApiException exception)
            {
                ShowError(MapApiError(exception));
            }
            finally
            {
                IsLoadingCatalog = false;
            }
        }

        partial void OnSelectedProfessionCategoryChanged(SkillCategoryOption? value) =>
            RefillOptions(ProfessionOptions, value);

        partial void OnSelectedSkillCategoryChanged(SkillCategoryOption? value) =>
            RefillOptions(SkillOptions, value);

        private static void RefillOptions(ObservableCollection<SkillOption> target, SkillCategoryOption? category)
        {
            target.Clear();
            if (category is null)
                return;
            foreach (var skill in category.Category.Skills)
                target.Add(new SkillOption(skill, LocalizationHelpers.LocalizedSkill(skill)));
        }

        // ---- step 1 → 2 ----

        [RelayCommand]
        private async Task ContinueFromBasicsAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(DisplayName)
                || DisplayName.Trim().Length < 2
                || SelectedProfession is null
                || SelectedEmirate is null)
            {
                ShowError(AppStrings.ErrorRequiredFields);
                return;
            }

            IsBusy = true;
            ClearError();
            try
            {
                _primarySkillId = SelectedProfession.Skill.Id;
                await _api.UpdateMyWorkerProfileAsync(new UpdateWorkerProfileRequestDto
                {
                    DisplayName = DisplayName.Trim(),
                    PrimarySkillId = _primarySkillId,
                    Emirate = SelectedEmirate.Value,
                    Area = string.IsNullOrWhiteSpace(Area) ? null : Area.Trim()
                });

                // The main profession is pre-added as a skill so the passport at
                // minimum matches it; the worker edits years and can add more.
                if (AddedSkills.All(s => s.SkillId != _primarySkillId.Value))
                    AddedSkills.Add(AddedSkillModel.FromOption(SelectedProfession, years: 0));

                Step = 2;
            }
            catch (ApiException exception) when (SessionErrors.IsExpiredSession(exception))
            {
                await SessionErrors.RouteToWelcomeAsync();
            }
            catch (ApiException exception)
            {
                ShowError(MapApiError(exception));
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---- step 2 → 3 ----

        [RelayCommand]
        private void AddSkill()
        {
            if (SelectedSkill is null)
                return;

            if (!int.TryParse(YearsText.Trim(), out var years) || years is < 0 or > 50)
            {
                ShowError(AppStrings.ErrorYearsInvalid);
                return;
            }

            if (AddedSkills.Any(s => s.SkillId == SelectedSkill.Skill.Id))
            {
                ShowError(AppStrings.ErrorDuplicateSkill);
                return;
            }

            ClearError();
            AddedSkills.Add(AddedSkillModel.FromOption(SelectedSkill, years));
            SelectedSkill = null;
            YearsText = string.Empty;
        }

        [RelayCommand]
        private void RemoveSkill(AddedSkillModel skill) => AddedSkills.Remove(skill);

        [RelayCommand]
        private async Task ContinueFromSkillsAsync()
        {
            if (IsBusy)
                return;

            if (AddedSkills.Count == 0)
            {
                ShowError(AppStrings.ErrorNeedOneSkill);
                return;
            }

            IsBusy = true;
            ClearError();
            try
            {
                await _api.SetMyWorkerSkillsAsync(
                    AddedSkills.Select(s => new SetWorkerSkillItemDto(s.SkillId, s.Years)).ToList());
                Step = 3;
            }
            catch (ApiException exception) when (SessionErrors.IsExpiredSession(exception))
            {
                await SessionErrors.RouteToWelcomeAsync();
            }
            catch (ApiException exception)
            {
                ShowError(MapApiError(exception));
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---- step 3 → done ----

        [RelayCommand]
        private async Task FinishAsync()
        {
            if (IsBusy)
                return;

            if (SelectedAvailability is null)
            {
                ShowError(AppStrings.ErrorRequiredFields);
                return;
            }

            int? salaryMin = ParseSalary(SalaryMinText);
            int? salaryMax = ParseSalary(SalaryMaxText);
            if ((SalaryMinText.Trim().Length > 0 && salaryMin is null)
                || (SalaryMaxText.Trim().Length > 0 && salaryMax is null)
                || (salaryMin is not null && salaryMax is not null && salaryMin > salaryMax))
            {
                ShowError(AppStrings.ErrorSalaryRange);
                return;
            }

            IsBusy = true;
            ClearError();
            try
            {
                var profile = await _api.UpdateMyWorkerProfileAsync(new UpdateWorkerProfileRequestDto
                {
                    Availability = SelectedAvailability.Value,
                    ExpectedSalaryMinAed = salaryMin,
                    ExpectedSalaryMaxAed = salaryMax,
                    About = string.IsNullOrWhiteSpace(About) ? null : About.Trim()
                });

                if (!profile.OnboardingComplete)
                {
                    // The server did not confirm completion — stay honest, stay here (§50).
                    ShowError(AppStrings.ErrorRequiredFields);
                    return;
                }

                await _session.ReloadUserAsync();
                await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.WorkerHome));
            }
            catch (ApiException exception) when (SessionErrors.IsExpiredSession(exception))
            {
                await SessionErrors.RouteToWelcomeAsync();
            }
            catch (ApiException exception)
            {
                ShowError(MapApiError(exception));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Back()
        {
            if (Step > 1 && !IsBusy)
                Step--;
        }

        // ---- helpers ----

        private static int? ParseSalary(string text) =>
            int.TryParse(text.Trim(), out var value) && value >= 0 ? value : null;

        internal static string MapApiError(ApiException exception)
        {
            if (exception.IsOffline)
                return AppStrings.ErrorOffline;
            return exception.Code switch
            {
                ApiClient.Codes.RateLimited => AppStrings.ErrorTooManyAttempts,
                "worker.salary_invalid" => AppStrings.ErrorSalaryRange,
                "worker.years_invalid" => AppStrings.ErrorYearsInvalid,
                "worker.skill_unknown" => AppStrings.ErrorGeneric,
                _ => AppStrings.ErrorGeneric
            };
        }

        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        private void ClearError()
        {
            ErrorMessage = null;
            HasError = false;
        }
    }
}
