using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation;
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

        public VacancyEditViewModel(IApiClient api) => _api = api;

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

        public IReadOnlyList<EnumOption> EmirateOptions => EnumDisplay.Emirates;

        [ObservableProperty]
        private EnumOption _selectedEmirate = EnumDisplay.Emirates[0];

        public ObservableCollection<SkillItemDto> SkillOptions { get; } = [];

        public ObservableCollection<object> SelectedSkills { get; } = [];

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("companyId", out var companyValue)
                && Guid.TryParse(companyValue?.ToString(), out var companyId))
            {
                _companyId = companyId;
            }

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
                await SessionErrors.RouteToWelcomeAsync();
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

            var salaryMinOk = string.IsNullOrWhiteSpace(SalaryMinText) || int.TryParse(SalaryMinText, out _);
            var salaryMaxOk = string.IsNullOrWhiteSpace(SalaryMaxText) || int.TryParse(SalaryMaxText, out _);
            if (!salaryMinOk || !salaryMaxOk || !int.TryParse(HeadcountText, out var headcount))
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
                    SalaryMinAed = int.TryParse(SalaryMinText, out var min) ? min : null,
                    SalaryMaxAed = int.TryParse(SalaryMaxText, out var max) ? max : null,
                    Headcount = headcount,
                    SkillIds = SelectedSkills.OfType<SkillItemDto>().Select(s => s.Id).ToList()
                };

                if (_vacancyId == Guid.Empty)
                    await _api.CreateVacancyAsync(_companyId, request);
                else
                    await _api.UpdateVacancyAsync(_companyId, _vacancyId, request);

                await Shell.Current.GoToAsync(".."); // back to the company's vacancy list
            }
            catch (ApiException exception) when (SessionErrors.IsExpiredSession(exception))
            {
                await SessionErrors.RouteToWelcomeAsync();
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
    }
}
