using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Jobs
{
    /// <summary>One row in "My applications" (worker side).</summary>
    public sealed record ApplicationRowModel(
        Guid Id,
        string VacancyTitle,
        string CompanyName,
        string Status,
        string StatusLabel,
        string AppliedDate,
        bool CanWithdraw);

    /// <summary>
    /// The worker's own applications with withdrawal while the employer's decision
    /// is open (terminal states hide the action — the server enforces it too).
    /// </summary>
    public partial class MyApplicationsViewModel : ObservableObject
    {
        private readonly IApiClient _api;

        public MyApplicationsViewModel(IApiClient api) => _api = api;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isLoaded;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _isEmpty;

        public ObservableCollection<ApplicationRowModel> Items { get; } = [];

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
                var applications = await _api.GetMyApplicationsAsync();
                Items.Clear();
                foreach (var application in applications)
                {
                    Items.Add(new ApplicationRowModel(
                        application.Id,
                        application.VacancyTitle,
                        application.CompanyName,
                        application.Status,
                        EnumDisplay.ApplicationStatus(application.Status),
                        application.CreatedAt.ToString("yyyy-MM-dd"),
                        application.Status is "Applied" or "Viewed" or "Shortlisted"));
                }

                IsEmpty = Items.Count == 0;
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
        private async Task WithdrawAsync(ApplicationRowModel? row)
        {
            if (row is null || IsBusy)
                return;

            try
            {
                await _api.WithdrawApplicationAsync(row.Id);
                await LoadAsync(); // refresh from the server — the list is its truth
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
        }

        /// <summary>Explicit back affordance: pages hide the Shell nav bar, so each
        /// screen carries its own parent route (absolute, like all app navigation).</summary>
        [RelayCommand]
        private Task BackAsync() => Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.WorkerHome));
    }
}
