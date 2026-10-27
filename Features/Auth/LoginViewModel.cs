using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation.Theming;
using KaamLink.Core.Text;
using KaamLink.Resources.Strings;
using Microsoft.Maui.Storage;

namespace KaamLink.Features.Auth
{
    /// <summary>
    /// Sign-in entry point: Supabase email OTP (with optional password creation),
    /// password sign-in for returning users, and phone OTP. A tickable terms
    /// acceptance gate applies to all paths. Google stays disabled until a real
    /// OAuth client and server-side token verification exist.
    /// </summary>
    public partial class LoginViewModel : ObservableObject, IQueryAttributable
    {
        private const string TermsAcceptedKey = "kaamlink.terms.accepted.v1";
        private const string PendingEmailKey = "kaamlink.email.pending.v1";

        private readonly IApiClient _api;
        private readonly ISessionService _session;
        private readonly IThemeService _theme;
        private readonly ISupabaseEmailAuthService _emailAuth;
        private readonly IEmailAuthLinkService _emailAuthLinks;
        private bool _sessionRestoreAttempted;
        private bool _countryDirectoryLoaded;
        private IReadOnlyList<CountryPhoneOption> _countries = Array.Empty<CountryPhoneOption>();
        private string? _pendingSupabaseAccessToken;
        private AuthTokensResponseDto? _pendingKaamLinkTokens;

        public LoginViewModel(
            IApiClient api,
            ISessionService session,
            IThemeService theme,
            ISupabaseEmailAuthService emailAuth,
            IEmailAuthLinkService emailAuthLinks)
        {
            _api = api;
            _session = session;
            _theme = theme;
            _emailAuth = emailAuth;
            _emailAuthLinks = emailAuthLinks;
            SyncThemeAffordance(theme.CurrentThemeId);
            RestoreLocalAuthState();
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

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _emailCode = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        [ObservableProperty]
        private bool _termsAccepted;

        [ObservableProperty]
        private bool _isEmailCodePending;

        [ObservableProperty]
        private bool _isPasswordSetupPending;

        [ObservableProperty]
        private bool _hasInfo;

        [ObservableProperty]
        private string _infoTitle = AppStrings.LoginEmailSentTitle;

        [ObservableProperty]
        private string? _infoMessage;

        public string SelectedCountryCode => SelectedCountry.CallingCodeLabel;

        partial void OnSelectedCountryChanged(CountryPhoneOption value) =>
            OnPropertyChanged(nameof(SelectedCountryCode));

        partial void OnCountrySearchTextChanged(string value) =>
            ApplyCountryFilter();

        partial void OnTermsAcceptedChanged(bool value)
        {
            try
            {
                Preferences.Default.Set(TermsAcceptedKey, value);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] terms preference could not be saved: {exception}");
            }
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("role", out var role)
                && role is string roleText
                && roleText is RoleNavigator.Worker or RoleNavigator.Employer)
            {
                Role = roleText;
            }
        }

        /// <summary>Loads offline country metadata, consumes any legacy link callback, then restores a saved session.</summary>
        public async Task InitializeAsync()
        {
            await EnsureCountryDirectoryLoadedAsync();
            await ProcessPendingEmailAuthCallbackAsync();
            if (_session.IsAuthenticated || IsPasswordSetupPending)
                return;
            await EnsureSessionRestoredAsync();
        }

        private void RestoreLocalAuthState()
        {
            try
            {
                TermsAccepted = Preferences.Default.Get(TermsAcceptedKey, false);
                Email = Preferences.Default.Get(PendingEmailKey, string.Empty);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] local email-auth state could not be restored: {exception}");
            }
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
        /// restore failures leave the sign-in form available instead of crashing boot.
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
            if (IsBusy || IsRestoringSession || IsPasswordSetupPending)
                return;

            if (!TermsAccepted)
            {
                ShowError(AppStrings.LoginTermsRequired);
                return;
            }

            var normalized = PhoneInput.TryNormalize(Phone, SelectedCountry);
            if (normalized is null)
            {
                ShowError(AppStrings.ErrorInvalidPhone);
                return;
            }

            IsBusy = true;
            ClearError();
            ClearInfo();
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

        [RelayCommand]
        private async Task SendEmailCodeAsync()
        {
            if (IsBusy || IsRestoringSession || IsPasswordSetupPending)
                return;

            if (!TermsAccepted)
            {
                ShowError(AppStrings.LoginTermsRequired, AppStrings.LoginEmailErrorTitle);
                return;
            }

            var email = Email.Trim();
            if (!SupabaseEmailAuthService.IsValidEmail(email))
            {
                ShowError(AppStrings.LoginEmailInvalid, AppStrings.LoginEmailErrorTitle);
                return;
            }

            IsBusy = true;
            ClearError();
            ClearInfo();
            try
            {
                await _emailAuth.SendOtpAsync(email);
                email = email.ToLowerInvariant();
                Email = email;
                EmailCode = string.Empty;
                IsEmailCodePending = true;
                SavePendingEmail(email);
                ShowInfo(AppStrings.LoginEmailSentTitle,
                    string.Format(AppStrings.LoginEmailSent, email));
            }
            catch (ApiException exception)
            {
                ShowError(MapError(exception), MapErrorTitle(exception, AppStrings.LoginEmailErrorTitle));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task VerifyEmailCodeAsync()
        {
            if (IsBusy || IsRestoringSession || IsPasswordSetupPending || !IsEmailCodePending)
                return;

            if (!TermsAccepted)
            {
                ShowError(AppStrings.LoginTermsRequired, AppStrings.LoginEmailErrorTitle);
                return;
            }

            var email = Email.Trim().ToLowerInvariant();
            var code = DigitText.Normalize(EmailCode).Trim();
            if (!SupabaseEmailAuthService.IsValidEmail(email)
                || code.Length is < 6 or > 8
                || !code.All(char.IsAsciiDigit))
            {
                ShowError(AppStrings.LoginEmailOtpInvalid, AppStrings.LoginEmailErrorTitle);
                return;
            }

            IsBusy = true;
            ClearError();
            ClearInfo();
            try
            {
                var session = await _emailAuth.VerifyOtpAsync(email, code);
                await CompleteSupabaseLoginAsync(session.AccessToken);
            }
            catch (ApiException exception)
            {
                ShowError(MapError(exception), MapErrorTitle(exception, AppStrings.LoginEmailErrorTitle));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SignInWithPasswordAsync()
        {
            if (IsBusy || IsRestoringSession || IsPasswordSetupPending)
                return;

            if (!TermsAccepted)
            {
                ShowError(AppStrings.LoginTermsRequired, AppStrings.LoginEmailErrorTitle);
                return;
            }

            var email = Email.Trim().ToLowerInvariant();
            if (!SupabaseEmailAuthService.IsValidEmail(email) || string.IsNullOrWhiteSpace(Password))
            {
                ShowError(AppStrings.LoginPasswordInvalidCredentials, AppStrings.LoginEmailErrorTitle);
                return;
            }

            IsBusy = true;
            ClearError();
            ClearInfo();
            try
            {
                var session = await _emailAuth.SignInWithPasswordAsync(email, Password);
                var tokens = await _api.ExchangeSupabaseAsync(session.AccessToken);
                await _session.CompleteLoginAsync(tokens);

                // If metadata was never written (older account), mark it now so
                // future OTP logins do not keep asking to create a password.
                if (tokens.RequiresPasswordSetup)
                {
                    try
                    {
                        await _emailAuth.SetPasswordAsync(session.AccessToken, Password);
                    }
                    catch (ApiException exception)
                    {
                        Console.WriteLine($"[KaamLink] password metadata update failed: {exception.Message}");
                    }
                }

                ClearPendingEmail();
                await NavigateAfterLoginAsync(tokens);
            }
            catch (ApiException exception)
            {
                ShowError(MapError(exception), MapErrorTitle(exception, AppStrings.LoginEmailErrorTitle));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SetPasswordAsync()
        {
            if (IsBusy || !IsPasswordSetupPending)
                return;

            if (!IsPasswordValid(Password))
            {
                ShowError(AppStrings.LoginPasswordInvalid, AppStrings.LoginEmailErrorTitle);
                return;
            }

            if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            {
                ShowError(AppStrings.LoginPasswordMismatch, AppStrings.LoginEmailErrorTitle);
                return;
            }

            if (string.IsNullOrWhiteSpace(_pendingSupabaseAccessToken))
            {
                ShowError(AppStrings.LoginEmailGeneric, AppStrings.LoginEmailErrorTitle);
                return;
            }

            IsBusy = true;
            ClearError();
            ClearInfo();
            try
            {
                await _emailAuth.SetPasswordAsync(_pendingSupabaseAccessToken, Password);
                await FinishPasswordSetupAsync();
            }
            catch (ApiException exception)
            {
                ShowError(MapError(exception), MapErrorTitle(exception, AppStrings.LoginEmailErrorTitle));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SkipPasswordAsync()
        {
            if (IsBusy || !IsPasswordSetupPending)
                return;

            ClearError();
            ClearInfo();
            await FinishPasswordSetupAsync();
        }

        /// <summary>
        /// Consumes a legacy deep-link callback once. OTP is now the primary flow,
        /// but existing link-based emails can still finish here.
        /// </summary>
        public async Task ProcessPendingEmailAuthCallbackAsync()
        {
            if (!_emailAuthLinks.TryTakePending(out var callback))
                return;

            if (callback.HasError || !callback.HasTokens)
            {
                ShowError(AppStrings.LoginEmailLinkInvalid, AppStrings.LoginEmailErrorTitle);
                return;
            }

            if (!TermsAccepted)
            {
                ShowError(AppStrings.LoginTermsRequired, AppStrings.LoginEmailErrorTitle);
                return;
            }

            IsBusy = true;
            ClearError();
            ClearInfo();
            try
            {
                await CompleteSupabaseLoginAsync(callback.AccessToken!);
            }
            catch (ApiException exception)
            {
                ShowError(MapError(exception), MapErrorTitle(exception, AppStrings.LoginEmailErrorTitle));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CompleteSupabaseLoginAsync(string supabaseAccessToken)
        {
            var tokens = await _api.ExchangeSupabaseAsync(supabaseAccessToken);
            await _session.CompleteLoginAsync(tokens);
            _pendingSupabaseAccessToken = supabaseAccessToken;
            _pendingKaamLinkTokens = tokens;
            IsEmailCodePending = false;
            ClearPendingEmail();

            if (tokens.RequiresPasswordSetup)
            {
                Password = string.Empty;
                ConfirmPassword = string.Empty;
                IsPasswordSetupPending = true;
                return;
            }

            await NavigateAfterLoginAsync(tokens);
        }

        private async Task FinishPasswordSetupAsync()
        {
            _pendingSupabaseAccessToken = null;
            IsPasswordSetupPending = false;
            Password = string.Empty;
            ConfirmPassword = string.Empty;

            var tokens = _pendingKaamLinkTokens;
            _pendingKaamLinkTokens = null;
            if (tokens is null)
            {
                await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.Welcome));
                return;
            }

            await NavigateAfterLoginAsync(tokens);
        }

        private Task NavigateAfterLoginAsync(AuthTokensResponseDto tokens) =>
            Shell.Current.GoToAsync(RoleNavigator.AbsoluteRouteFor(tokens.User, Role));

        private static bool IsPasswordValid(string password) =>
            password.Length >= 8
            && password.Any(char.IsLetter)
            && password.Any(char.IsAsciiDigit);

        private void SavePendingEmail(string email)
        {
            try
            {
                Preferences.Default.Set(PendingEmailKey, email);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] pending email could not be saved: {exception}");
            }
        }

        private void ClearPendingEmail()
        {
            try
            {
                Preferences.Default.Remove(PendingEmailKey);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] pending email could not be cleared: {exception}");
            }
        }

        internal static string MapErrorTitle(ApiException exception, string inputTitle)
        {
            if (exception.IsNoInternet)
                return AppStrings.ErrorNoInternetTitle;
            if (exception.IsMisconfigured)
                return AppStrings.ErrorApiNotConfiguredTitle;
            if (IsEmailAuthCode(exception.Code))
                return AppStrings.LoginEmailErrorTitle;
            return exception.IsOffline ? AppStrings.ErrorOfflineTitle : inputTitle;
        }

        internal static string MapError(ApiException exception)
        {
            if (exception.IsNoInternet)
                return AppStrings.ErrorNoInternet;
            if (exception.IsMisconfigured)
                return AppStrings.ErrorApiNotConfigured;

            // Client-side email errors have no HTTP status. Match their codes
            // before the generic offline fallback, or they are reported as
            // "server unavailable" instead of actionable copy.
            if (exception.Code is not null)
            {
                var emailMessage = MapEmailAuthCode(exception.Code);
                if (emailMessage is not null)
                    return emailMessage;
            }

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

        private static bool IsEmailAuthCode(string? code) => MapEmailAuthCode(code) is not null;

        private static string? MapEmailAuthCode(string? code) => code switch
        {
            ApiClient.Codes.EmailInvalid => AppStrings.LoginEmailInvalid,
            ApiClient.Codes.TermsRequired => AppStrings.LoginTermsRequired,
            ApiClient.Codes.SupabaseNotConfigured => AppStrings.LoginEmailNotConfigured,
            ApiClient.Codes.SupabaseServerNotConfigured => AppStrings.LoginEmailNotConfigured,
            ApiClient.Codes.SupabaseRateLimited => AppStrings.LoginEmailRateLimited,
            ApiClient.Codes.SupabaseInvalidLink => AppStrings.LoginEmailLinkInvalid,
            ApiClient.Codes.SupabaseTokenRequired => AppStrings.LoginEmailLinkInvalid,
            ApiClient.Codes.SupabaseOtpInvalid => AppStrings.LoginEmailOtpInvalid,
            ApiClient.Codes.SupabasePasswordSetFailed => AppStrings.LoginPasswordSetFailed,
            ApiClient.Codes.SupabaseInvalidCredentials => AppStrings.LoginPasswordInvalidCredentials,
            ApiClient.Codes.SupabaseUnavailable => AppStrings.LoginEmailGeneric,
            ApiClient.Codes.SupabaseSendFailed => AppStrings.LoginEmailGeneric,
            _ => null
        };

        private void ShowError(string message, string? title = null)
        {
            ClearInfo();
            ErrorTitle = title ?? AppStrings.LoginPhoneErrorTitle;
            ErrorMessage = message;
            HasError = true;
        }

        private void ClearError()
        {
            ErrorMessage = null;
            HasError = false;
        }

        private void ShowInfo(string title, string message)
        {
            ClearError();
            InfoTitle = title;
            InfoMessage = message;
            HasInfo = true;
        }

        private void ClearInfo()
        {
            InfoMessage = null;
            HasInfo = false;
        }
    }
}
