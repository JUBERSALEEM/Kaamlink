using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Employer
{
    /// <summary>Presentation wrapper for a company on the employer home (labels pre-localized).</summary>
    public sealed record CompanyCardModel(
        string Name,
        string CategoryEmirateLine,
        string RoleText,
        string VerificationState,
        string VerificationLabel,
        string? VerificationReason,
        bool HasReason,
        bool IsPending);

    /// <summary>
    /// Employer experience root: the caller's companies with their REAL verification
    /// state from the server (Pending/Verified/Rejected). Vacancy management lands
    /// next phase — until then the roadmap note speaks instead of fake UI (§50).
    /// </summary>
    public partial class EmployerHomeViewModel : ObservableObject
    {
        private readonly IApiClient _api;
        private readonly ISessionService _session;

        public EmployerHomeViewModel(IApiClient api, ISessionService session)
        {
            _api = api;
            _session = session;
        }

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isLoaded;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string? _errorMessage;

        public ObservableCollection<CompanyCardModel> Companies { get; } = [];

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                var companies = await _api.GetMyCompaniesAsync();
                if (companies.Count == 0)
                {
                    // No membership (anymore): onboarding is the honest destination.
                    await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.EmployerOnboarding));
                    return;
                }

                Companies.Clear();
                foreach (var company in companies)
                {
                    var categoryLine = $"{EnumDisplay.BusinessCategory(company.Category)} · {EnumDisplay.Emirate(company.Emirate)}";
                    var hasReason = !string.IsNullOrWhiteSpace(company.VerificationReason);
                    Companies.Add(new CompanyCardModel(
                        company.Name,
                        categoryLine,
                        string.Format(AppStrings.MembershipRoleFormat, EnumDisplay.CompanyRole(company.Role)),
                        company.VerificationState,
                        EnumDisplay.VerificationState(company.VerificationState),
                        hasReason ? string.Format(AppStrings.VerificationReasonFormat, company.VerificationReason) : null,
                        hasReason,
                        company.VerificationState == "Pending"));
                }

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

        [RelayCommand]
        private async Task SwitchToWorkerAsync()
        {
            var user = await _session.ReloadUserAsync() ?? _session.CurrentUser;
            await Shell.Current.GoToAsync(user is not null
                ? RoleNavigator.AbsoluteRouteFor(user, RoleNavigator.Worker)
                : RoleNavigator.LoginRouteFor(RoleNavigator.Worker));
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            await _session.LogoutAsync();
            await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.Welcome));
        }
    }
}
