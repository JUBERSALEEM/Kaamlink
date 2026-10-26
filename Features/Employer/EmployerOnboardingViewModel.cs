using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Employer
{
    /// <summary>
    /// Employer onboarding: register the hiring company. The company starts as
    /// Pending (reviewed by platform admins) — the page copy says so up front and
    /// the submit button is "Submit for review", never implying instant verification.
    /// </summary>
    public partial class EmployerOnboardingViewModel : ObservableObject
    {
        private readonly IApiClient _api;
        private readonly ISessionService _session;

        public EmployerOnboardingViewModel(IApiClient api, ISessionService session)
        {
            _api = api;
            _session = session;
        }

        [ObservableProperty]
        private string _companyName = string.Empty;

        public IReadOnlyList<EnumOption> BusinessCategories => EnumDisplay.BusinessCategories;

        [ObservableProperty]
        private EnumOption? _selectedCategory;

        public IReadOnlyList<EnumOption> Emirates => EnumDisplay.Emirates;

        [ObservableProperty]
        private EnumOption? _selectedEmirate;

        [ObservableProperty]
        private string _area = string.Empty;

        [ObservableProperty]
        private string _tradeLicenseNumber = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _hasError;

        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(CompanyName)
                || CompanyName.Trim().Length < 2
                || SelectedCategory is null
                || SelectedEmirate is null)
            {
                ShowError(AppStrings.ErrorRequiredFields);
                return;
            }

            IsBusy = true;
            ClearError();
            try
            {
                await _api.CreateCompanyAsync(new CreateCompanyRequestDto
                {
                    Name = CompanyName.Trim(),
                    Category = SelectedCategory.Value,
                    Emirate = SelectedEmirate.Value,
                    Area = string.IsNullOrWhiteSpace(Area) ? null : Area.Trim(),
                    TradeLicenseNumber = string.IsNullOrWhiteSpace(TradeLicenseNumber) ? null : TradeLicenseNumber.Trim()
                });

                await _session.ReloadUserAsync();
                await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.EmployerHome));
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

        private static string MapApiError(ApiException exception)
        {
            if (exception.IsOffline)
                return AppStrings.ErrorOffline;
            return exception.Code switch
            {
                ApiClient.Codes.RateLimited => AppStrings.ErrorTooManyAttempts,
                "company.too_many" => AppStrings.ErrorTooManyCompanies,
                "company.name_invalid" => AppStrings.ErrorCompanyNameInvalid,
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

        /// <summary>Leave registration and return to the role chooser (nothing is lost
        /// on the server until Submit).</summary>
        [RelayCommand]
        private Task BackAsync() => Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.Welcome));
    }
}
