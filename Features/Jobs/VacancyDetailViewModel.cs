using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Jobs
{
    /// <summary>
    /// Vacancy detail + apply. IQueryAttributable by hand: ?vacancyId= arrives as a
    /// string. The apply button disappears once the worker has applied — the server
    /// still enforces one application; the client only reflects its verdict.
    /// </summary>
    public partial class VacancyDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IApiClient _api;
        private Guid _vacancyId;

        public VacancyDetailViewModel(IApiClient api) => _api = api;

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
        private string _companyLine = string.Empty;

        [ObservableProperty]
        private string _companyVerificationState = string.Empty;

        [ObservableProperty]
        private string _companyVerificationLabel = string.Empty;

        [ObservableProperty]
        private string _locationLine = string.Empty;

        [ObservableProperty]
        private string _salaryText = string.Empty;

        [ObservableProperty]
        private string _headcountText = string.Empty;

        [ObservableProperty]
        private string _skillsLine = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _hasDescription;

        [ObservableProperty]
        private string _coverNote = string.Empty;

        /// <summary>Raw API status of the caller's own application (drives pill color).</summary>
        [ObservableProperty]
        private string? _ownApplicationStatus;

        [ObservableProperty]
        private string _ownApplicationLabel = string.Empty;

        [ObservableProperty]
        private bool _hasOwnApplication;

        [ObservableProperty]
        private bool _canApply;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("vacancyId", out var value)
                && Guid.TryParse(value?.ToString(), out var id))
            {
                _vacancyId = id;
            }
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy || _vacancyId == Guid.Empty)
                return;

            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                var vacancy = await _api.GetVacancyAsync(_vacancyId);

                Title = vacancy.Title;
                CompanyLine = vacancy.CompanyName;
                CompanyVerificationState = vacancy.CompanyVerificationState;
                CompanyVerificationLabel = EnumDisplay.VerificationState(vacancy.CompanyVerificationState);
                LocationLine = string.IsNullOrWhiteSpace(vacancy.Area)
                    ? EnumDisplay.Emirate(vacancy.Emirate)
                    : $"{EnumDisplay.Emirate(vacancy.Emirate)} · {vacancy.Area}";
                SalaryText = vacancy.SalaryMinAed is not null && vacancy.SalaryMaxAed is not null
                    ? string.Format(AppStrings.FeedCardSalaryFormat, vacancy.SalaryMinAed, vacancy.SalaryMaxAed)
                    : AppStrings.FeedCardSalaryHidden;
                HeadcountText = string.Format(AppStrings.HeadcountFormat, vacancy.Headcount);
                SkillsLine = string.Join(" · ", vacancy.Skills.Select(s => LocalizationHelpers.LocalizedSkill(s)));
                Description = vacancy.Description ?? string.Empty;
                HasDescription = !string.IsNullOrWhiteSpace(Description);

                ApplyOwnStatus(vacancy.OwnApplicationStatus);

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

        [RelayCommand]
        private async Task ApplyAsync()
        {
            if (IsBusy || _vacancyId == Guid.Empty)
                return;

            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                var application = await _api.ApplyAsync(
                    _vacancyId,
                    string.IsNullOrWhiteSpace(CoverNote) ? null : CoverNote.Trim());
                ApplyOwnStatus(application.Status);
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
                    ApiClient.Codes.AlreadyApplied => AppStrings.ErrorAlreadyApplied,
                    ApiClient.Codes.OnboardingIncomplete or ApiClient.Codes.ProfileRequired =>
                        AppStrings.ErrorOnboardingIncomplete,
                    _ => exception.IsOffline ? AppStrings.ErrorOffline : AppStrings.ErrorGeneric
                };
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyOwnStatus(string? status)
        {
            OwnApplicationStatus = status;
            HasOwnApplication = status is not null;
            OwnApplicationLabel = status is null
                ? string.Empty
                : string.Format(AppStrings.AppliedBadgeFormat, EnumDisplay.ApplicationStatus(status));
            CanApply = status is null;
        }

        /// <summary>Explicit back affordance: pages hide the Shell nav bar, so each
        /// screen carries its own parent route (absolute, like all app navigation).</summary>
        [RelayCommand]
        private Task BackAsync() => Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.VacancyFeed));
    }
}
