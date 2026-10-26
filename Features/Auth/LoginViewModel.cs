using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation.Theming;
using KaamLink.Core.Text;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Auth
{
    /// <summary>
    /// Global phone sign-in entry point. It keeps the selected region through OTP;
    /// a returning session proceeds to role choice without assuming a role.
    /// Google auth must be backed by a real server-side identity exchange.
    /// </summary>
    public partial class LoginViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IApiClient _api;
        private readonly ISessionService _session;
        private readonly IThemeService _theme;
        private bool _sessionRestoreAttempted;
        private bool _countryDirectoryLoaded;
        private IReadOnlyList<CountryPhoneOption> _countries = Array.Empty<CountryPhoneOption>();

        public LoginViewModel(IApiClient api, ISessionService session, IThemeService theme)
        {
            _api = api;
            _session = session;
            _theme = theme;
            SyncThemeAffordance(theme.CurrentThemeId);
        }

        [ObservableProperty]
        private string _role = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private CountryPhoneOption _selectedCountry = new("AE", "971", "0", false,
            "United Arab Emirates", "United Arab Emirates");

        [ObservableProperty]
        private IReadOnlyList<CountryPhoneOption> _filteredCountries = Array.Empty<CountryPhoneOption>();

        [ObservableProperty]
        private string _countrySearchText = string.Empty;

        [ObservableProperty]
        private bool _isCountryPickerVisible;

        [ObservableProperty]
        private string _themeToggleLabel = AppStrings.ThemeSwitchToDusk;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isRestoringSession;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorTitle = AppStrings.LoginPhoneErrorTitle;

        public string SelectedCountryCode => SelectedCountry.CallingCodeLabel;

        partial void OnSelectedCountryChanged(CountryPhoneOption value) =>
            OnPropertyChanged(nameof(SelectedCountryCode));

        partial void OnCountrySearchTextChanged(string value) =>
            ApplyCountryFilter();

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("role", out var role)
                && role is string roleText
                && roleText is RoleNavigator.Worker or RoleNavigator.Employer)
            {
                Role = roleText;
            }
        }

        /// <summary>Loads offline country metadata and restores any saved session once.</summary>
        public async Task InitializeAsync()
        {
            await EnsureCountryDirectoryLoadedAsync();
            await EnsureSessionRestoredAsync();
        }

        private async Task EnsureCountryDirectoryLoadedAsync()
        {
            if (_countryDirectoryLoaded)
                return;

            _countryDirectoryLoaded = true;
            try
            {
                _countries = await CountryPhoneDirectory.LoadAsync();
                var preferredIso = CountryPhoneDirectory.GetDeviceRegionIsoCode();
                SelectedCountry = _countries.FirstOrDefault(country =>
                    string.Equals(country.IsoCode, preferredIso, StringComparison.OrdinalIgnoreCase))
                    ?? _countries.FirstOrDefault(country => country.IsoCode == "AE")
                    ?? _countries[0];
                ApplyCountryFilter();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] offline country directory could not be loaded: {exception}");
                _countries = [SelectedCountry];
                ApplyCountryFilter();
            }
        }

        private void ApplyCountryFilter()
        {
            var query = CountrySearchText.Trim();
            FilteredCountries = string.IsNullOrEmpty(query)
                ? _countries
                : _countries.Where(country => country.SearchText.Contains(
                        query, StringComparison.CurrentCultureIgnoreCase))
                    .ToArray();
        }

        [RelayCommand]
        private void OpenCountryPicker()
        {
            CountrySearchText = string.Empty;
            ApplyCountryFilter();
            IsCountryPickerVisible = true;
        }

        [RelayCommand]
        private void CloseCountryPicker() => IsCountryPickerVisible = false;

        [RelayCommand]
        private void SelectCountry(CountryPhoneOption? country)
        {
            if (country is null || !_countries.Contains(country))
                return;

            SelectedCountry = country;
            IsCountryPickerVisible = false;
            CountrySearchText = string.Empty;
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            var nextThemeId = _theme.Toggle();
            SyncThemeAffordance(nextThemeId);
        }

        private void SyncThemeAffordance(string themeId) =>
            ThemeToggleLabel = themeId == ThemeCatalog.ClayDusk
                ? AppStrings.ThemeSwitchToLight
                : AppStrings.ThemeSwitchToDusk;

        /// <summary>
        /// A returning session bypasses the form and reaches role choice. Offline
        /// restore failures leave the phone form available instead of crashing boot.
        /// </summary>
        private async Task EnsureSessionRestoredAsync()
        {
            if (_sessionRestoreAttempted)
                return;

            _sessionRestoreAttempted = true;
            if (_session.IsAuthenticated)
            {
                await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.Welcome));
                return;
            }

            IsRestoringSession = true;
            try
            {
                await _session.RestoreAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] saved session restore failed: {exception}");
            }
            finally
            {
                IsRestoringSession = false;
            }

            if (_session.IsAuthenticated)
                await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.Welcome));
        }

        [RelayCommand]
        private async Task SendCodeAsync()
        {
            if (IsBusy || IsRestoringSession)
                return;

            var normalized = PhoneInput.TryNormalize(Phone, SelectedCountry);
            if (normalized is null)
            {
                ShowError(AppStrings.ErrorInvalidPhone);
                return;
            }

            IsBusy = true;
            ClearError();
            try
            {
                var response = await _api.RequestOtpAsync(normalized);
                await Shell.Current.GoToAsync(
                    RoleNavigator.OtpRouteFor(normalized, Role, response.ResendCooldownSeconds));
            }
            catch (ApiException exception)
            {
                ShowError(MapError(exception), MapErrorTitle(exception, AppStrings.LoginPhoneErrorTitle));
            }
            finally
            {
                IsBusy = false;
            }
        }

        internal static string MapErrorTitle(ApiException exception, string inputTitle)
        {
            if (exception.IsNoInternet)
                return AppStrings.ErrorNoInternetTitle;
            if (exception.IsMisconfigured)
                return AppStrings.ErrorApiNotConfiguredTitle;
            return exception.IsOffline ? AppStrings.ErrorOfflineTitle : inputTitle;
        }

        internal static string MapError(ApiException exception)
        {
            if (exception.IsNoInternet)
                return AppStrings.ErrorNoInternet;
            if (exception.IsMisconfigured)
                return AppStrings.ErrorApiNotConfigured;
            if (exception.IsOffline)
                return AppStrings.ErrorOffline;
            return exception.Code switch
            {
                ApiClient.Codes.RateLimited => AppStrings.ErrorTooManyAttempts,
                ApiClient.Codes.OtpInvalid => AppStrings.ErrorInvalidCode,
                ApiClient.Codes.OtpNotFoundOrExpired => AppStrings.ErrorInvalidCode,
                ApiClient.Codes.OtpLocked => AppStrings.ErrorTooManyAttempts,
                ApiClient.Codes.OtpResendCooldown => AppStrings.ErrorTooManyAttempts,
                ApiClient.Codes.PhoneInvalid => AppStrings.ErrorInvalidPhone,
                _ => AppStrings.ErrorGeneric
            };
        }

        private void ShowError(string message, string? title = null)
        {
            ErrorTitle = title ?? AppStrings.LoginPhoneErrorTitle;
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
