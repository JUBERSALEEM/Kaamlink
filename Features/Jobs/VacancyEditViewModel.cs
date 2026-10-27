using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation;
using KaamLink.Core.Text;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Jobs
{
    /// <summary>
    /// Vacancy create/edit form (?companyId=, optional ?vacancyId=). The server is
    /// the validation authority; client-side checks only shorten round trips.
    /// Known V1 limitation (documented): editing re-asks for the required skills —
    /// the employer list endpoint does not return them — so the form deliberately
    /// blocks saving without a fresh selection rather than clobbering silently.
    /// </summary>
    public partial class VacancyEditViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IApiClient _api;
        private Guid _companyId;
        private Guid _vacancyId;

        public VacancyEditViewModel(IApiClient api)
        {
            _api = api;
            SelectedSkills.CollectionChanged += (_, _) => SelectedSkillsText = string.Format(
                AppStrings.SkillsSelectedCountFormat,
                SelectedSkills.Count);
        }

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isLoaded;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _area = string.Empty;

        [ObservableProperty]
        private string _salaryMinText = string.Empty;

        [ObservableProperty]
        private string _salaryMaxText = string.Empty;

        [ObservableProperty]
        private string _headcountText = "1";

        [ObservableProperty]
        private string _pageTitle = string.Empty;

        /// <summary>Carried from the vacancy list so the form can return there.</summary>
        [ObservableProperty]
        private string _companyName = string.Empty;

        public IReadOnlyList<EnumOption> EmirateOptions => EnumDisplay.Emirates;

        [ObservableProperty]
        private EnumOption _selectedEmirate = EnumDisplay.Emirates[0];

        public ObservableCollection<SkillItemDto> SkillOptions { get; } = [];

        public ObservableCollection<object> SelectedSkills { get; } = [];

        /// <summary>Selected-skill count shown under the skills picker (display only).</summary>
        [ObservableProperty]
        private string _selectedSkillsText = string.Empty;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("companyId", out var companyValue)
                && Guid.TryParse(companyValue?.ToString(), out var companyId))
            {
                _companyId = companyId;
            }

            if (query.TryGetValue("companyName", out var companyNameValue))
                CompanyName = Uri.UnescapeDataString(companyNameValue?.ToString() ?? string.Empty);

            if (query.TryGetValue("vacancyId", out var vacancyValue)
                && Guid.TryParse(vacancyValue?.ToString(), out var vacancyId))
            {
                _vacancyId = vacancyId;
            }

            PageTitle = _vacancyId == Guid.Empty
                ? AppStrings.VacancyEditTitleNew
                : AppStrings.VacancyEditTitleEdit;
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy || SkillOptions.Count > 0)
                return;

            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                var catalog = await _api.GetSkillsCatalogAsync();
                SkillOptions.Clear();
                foreach (var skill in catalog.Categories.SelectMany(c => c.Skills))
                    SkillOptions.Add(skill);

                if (_vacancyId != Guid.Empty)
                    await PrefillAsync();

                IsLoaded = true;
            }
            catch (ApiException exception) when (SessionErrors.IsExpiredSession(exception))
            {
                await SessionErrors.RouteToLoginAsync();
            }
            catch (ApiException exception)
            {
                HasError = true;
                ErrorMessage = exception.IsOffline ? AppStrings.ErrorOffline : AppStrings.ErrorGeneric;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Prefill scalar fields in edit mode (skills re-selected manually — see class remarks).</summary>
        private async Task PrefillAsync()
        {
            var vacancies = await _api.GetCompanyVacanciesAsync(_companyId);
            var vacancy = vacancies.FirstOrDefault(v => v.Id == _vacancyId)
                ?? throw new ApiException(AppStrings.ErrorGeneric, 404, "jobs.vacancy_not_found");

            Title = vacancy.Title;
            SelectedEmirate = EmirateOptions.FirstOrDefault(o => o.Value == vacancy.Emirate)
                ?? EmirateOptions[0];
            Area = vacancy.Area ?? string.Empty;
            SalaryMinText = vacancy.SalaryMinAed?.ToString() ?? string.Empty;
            SalaryMaxText = vacancy.SalaryMaxAed?.ToString() ?? string.Empty;
            HeadcountText = vacancy.Headcount.ToString();
            // Description is not part of the employer summary DTO; left empty on purpose.
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy || _companyId == Guid.Empty)
                return;

            // Parse once into the values that are persisted: re-parsing at build
            // time could drift from what was validated. Digits are normalized
            // first so Arabic-Indic input from an Arabic keyboard is accepted.
            var salaryMinText = DigitText.Normalize(SalaryMinText).Trim();
            var salaryMaxText = DigitText.Normalize(SalaryMaxText).Trim();
            var salaryMinOk = string.IsNullOrWhiteSpace(salaryMinText)
                || int.TryParse(salaryMinText, out _);
            var salaryMaxOk = string.IsNullOrWhiteSpace(salaryMaxText)
                || int.TryParse(salaryMaxText, out _);
            if (!salaryMinOk || !salaryMaxOk
                || !int.TryParse(DigitText.Normalize(HeadcountText).Trim(), out var headcount))
            {
                HasError = true;
                ErrorMessage = AppStrings.FieldInvalidNumber;
                return;
            }

            HasError = false;
            ErrorMessage = null;
            IsBusy = true;
            try
            {
                var request = new UpsertVacancyRequestDto
                {
                    Title = Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    Emirate = SelectedEmirate.Value,
                    Area = string.IsNullOrWhiteSpace(Area) ? null : Area.Trim(),
                    SalaryMinAed = int.TryParse(salaryMinText, out var min) ? min : null,
                    SalaryMaxAed = int.TryParse(salaryMaxText, out var max) ? max : null,
                    Headcount = headcount,
                    SkillIds = SelectedSkills.OfType<SkillItemDto>().Select(s => s.Id).ToList()
                };

                if (_vacancyId == Guid.Empty)
                    await _api.CreateVacancyAsync(_companyId, request);
                else
                    await _api.UpdateVacancyAsync(_companyId, _vacancyId, request);

                await ReturnToListAsync(); // back to the company's vacancy list
            }
            catch (ApiException exception) when (SessionErrors.IsExpiredSession(exception))
            {
                await SessionErrors.RouteToLoginAsync();
            }
            catch (ApiException exception)
            {
                HasError = true;
                ErrorMessage = exception.Code switch
                {
                    ApiClient.Codes.SalaryPairRequired or ApiClient.Codes.SalaryInvalid =>
                        AppStrings.FieldInvalidNumber,
                    _ => exception.IsOffline ? AppStrings.ErrorOffline : AppStrings.ErrorGeneric
                };
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Explicit back affordance (the Shell nav bar is hidden on this page).</summary>
        [RelayCommand]
        private Task BackAsync() => ReturnToListAsync();

        private Task ReturnToListAsync() =>
            Shell.Current.GoToAsync(
                $"{AppRoutes.Absolute(AppRoutes.CompanyVacancies)}" +
                $"?companyId={_companyId}&companyName={Uri.EscapeDataString(CompanyName)}");
    }
}
