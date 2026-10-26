using KaamLink.Core.Api;
using KaamLink.Core.Text;
using KaamLink.Core.Navigation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Auth
{
    /// <summary>
    /// OTP verification. On success the session adopts the identity and the user
    /// continues to their intended role destination (home or onboarding) — decided
    /// by RoleNavigator from the server's own summary, never from client flags.
    /// IQueryAttributable by hand: query values arrive as strings (cooldown too).
    /// </summary>
    public partial class OtpViewModel : ObservableObject, IQueryAttributable, IDisposable
    {
        // Matches the server's default resend cooldown; only used as a fallback when
        // the login page could not pass the configured value (deep navigations).
        private const int InitialCooldownFallbackSeconds = 60;

        private readonly IApiClient _api;
        private readonly ISessionService _session;
        private IDispatcherTimer? _cooldownTimer;
        private bool _cooldownInitialized;

        public OtpViewModel(IApiClient api, ISessionService session)
        {
            _api = api;
            _session = session;
        }

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _role = string.Empty;

        [ObservableProperty]
        private string _code = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _hasError;

        /// <summary>
        /// Banner title. Kept separate from the body so a network failure is not
        /// reported as "That code isn't right" when the code was never checked.
        /// </summary>
        [ObservableProperty]
        private string _errorTitle = AppStrings.OtpErrorTitle;

        [ObservableProperty]
        private int _secondsRemaining;

        [ObservableProperty]
        private string _resendText = AppStrings.ResendCta;

        public bool CanResend => SecondsRemaining <= 0 && !IsBusy;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            // The route carries the number escaped (?phone=%2B971...). Shell only
            // decodes query values delivered through QueryPropertyAttribute, not
            // through IQueryAttributable, so without this the screen would display
            // and submit "%2B971501234567" and every OTP verification would fail.
            if (query.TryGetValue("phone", out var phone) && phone is string phoneText)
                Phone = Uri.UnescapeDataString(phoneText);
            if (query.TryGetValue("role", out var role) && role is string roleText && roleText.Length > 0)
                Role = roleText;
            if (query.TryGetValue("cooldown", out var cooldown)
                && int.TryParse(cooldown?.ToString(), out var seconds)
                && seconds > 0
                && !_cooldownInitialized)
            {
                _cooldownInitialized = true;
                StartCooldown(seconds);
            }
        }

        public void OnAppearing()
        {
            // Deep landings without a cooldown value: fail safe without magic behavior.
            if (!_cooldownInitialized)
            {
                _cooldownInitialized = true;
                StartCooldown(InitialCooldownFallbackSeconds);
            }
        }

        [RelayCommand]
        private async Task VerifyAsync()
        {
            if (IsBusy)
                return;

            // The API expects six ASCII digits; an Arabic-locale numeric keyboard
            // produces Arabic-Indic digits that would otherwise be rejected.
            var code = DigitText.Normalize(Code).Trim();
            if (code.Length != 6 || !code.All(char.IsAsciiDigit))
            {
                ShowError(AppStrings.ErrorInvalidCode);
                return;
            }

            IsBusy = true;
            OnPropertyChanged(nameof(CanResend));
            ClearError();
            try
            {
                var tokens = await _api.VerifyOtpAsync(Phone, code);
                await _session.CompleteLoginAsync(tokens);
                StopTimer();
                await Shell.Current.GoToAsync(RoleNavigator.AbsoluteRouteFor(tokens.User, Role));
            }
            catch (ApiException exception)
            {
                // One shared mapping table for every auth error surface.
                ShowError(
                    LoginViewModel.MapError(exception),
                    LoginViewModel.MapErrorTitle(exception, AppStrings.OtpErrorTitle));
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanResend));
            }
        }

        [RelayCommand]
        private async Task ResendAsync()
        {
            if (IsBusy || !CanResend)
                return;

            IsBusy = true;
            OnPropertyChanged(nameof(CanResend));
            ClearError();
            try
            {
                var response = await _api.RequestOtpAsync(Phone);
                StartCooldown(response.ResendCooldownSeconds);
            }
            catch (ApiException exception)
            {
                ShowError(
                    LoginViewModel.MapError(exception),
                    LoginViewModel.MapErrorTitle(exception, AppStrings.OtpErrorTitle));
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanResend));
            }
        }

        private void StartCooldown(int seconds)
        {
            StopTimer();

            if (seconds <= 0)
            {
                SecondsRemaining = 0;
                ResendText = AppStrings.ResendCta;
                OnPropertyChanged(nameof(CanResend));
                return;
            }

            SecondsRemaining = seconds;
            ResendText = string.Format(AppStrings.ResendWaitFormat, seconds);
            OnPropertyChanged(nameof(CanResend));

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
                return;

            _cooldownTimer = dispatcher.CreateTimer();
            _cooldownTimer.Interval = TimeSpan.FromSeconds(1);
            _cooldownTimer.Tick += OnCooldownTick;
            _cooldownTimer.Start();
        }

        private void OnCooldownTick(object? sender, EventArgs e)
        {
            SecondsRemaining--;
            if (SecondsRemaining <= 0)
            {
                StopTimer();
                SecondsRemaining = 0;
                ResendText = AppStrings.ResendCta;
            }
            else
            {
                ResendText = string.Format(AppStrings.ResendWaitFormat, SecondsRemaining);
            }

            OnPropertyChanged(nameof(CanResend));
        }

        private void StopTimer()
        {
            if (_cooldownTimer is null)
                return;
            _cooldownTimer.Tick -= OnCooldownTick;
            _cooldownTimer.Stop();
            _cooldownTimer = null;
        }

        private void ShowError(string message, string? title = null)
        {
            ErrorTitle = title ?? AppStrings.OtpErrorTitle;
            ErrorMessage = message;
            HasError = true;
        }

        private void ClearError()
        {
            ErrorMessage = null;
            HasError = false;
        }

        public void Dispose() => StopTimer();

        /// <summary>Back to the phone step so a mistyped number can be corrected
        /// (the page's own copy tells the user to do exactly this).</summary>
        [RelayCommand]
        private Task BackAsync() => Shell.Current.GoToAsync(RoleNavigator.LoginRouteFor(Role));
    }
}
