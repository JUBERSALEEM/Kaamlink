using KaamLink.Core.Api;
using KaamLink.Core.Text;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Auth
{
    /// <summary>
    /// Phone-first sign-in. Validates the UAE mobile locally, asks the API for an
    /// OTP, then hands off to the OTP page. The intended role (worker/employer)
    /// rides along as a query value so the flow can continue after verify.
    /// IQueryAttributable is implemented by hand (not the source generator) so
    /// parsing/defaults stay explicit and testable.
    /// </summary>
    public partial class LoginViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IApiClient _api;

        public LoginViewModel(IApiClient api) => _api = api;

        [ObservableProperty]
        private string _role = RoleNavigator.Worker;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _hasError;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("role", out var role) && role is string roleText && roleText.Length > 0)
                Role = roleText;
        }

        [RelayCommand]
        private async Task SendCodeAsync()
        {
            if (IsBusy)
                return;

            var normalized = PhoneInput.TryNormalize(Phone);
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
                ShowError(MapError(exception));
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Returns to role selection. Additive UI-only command: sign-in is reached by
        /// absolute route, so the platform has no stack to pop and the screen needs an
        /// explicit way back (the mockup's app-bar back affordance).
        /// </summary>
        [RelayCommand]
        private async Task BackToRolesAsync() =>
            await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.Welcome));

        internal static string MapError(ApiException exception)
        {
            if (exception.IsOffline)
                return AppStrings.ErrorOffline;
            return exception.Code switch
            {
                ApiClient.Codes.RateLimited => AppStrings.ErrorTooManyAttempts,
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
