using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Jobs
{
    /// <summary>One vacancy row on the employer's management list (action visibility precomputed).</summary>
    public sealed record EmployerVacancyRowModel(
        Guid Id,
        string Title,
        string Status,
        string StatusLabel,
        string MetaLine,
        string ApplicationsCountText,
        bool CanActivate,
        bool CanPause,
        bool CanClose);

    /// <summary>
    /// Employer vacancy list for one company (?companyId=&amp;companyName=). Every
    /// action hits the server, which re-checks membership, role and verification —
    /// the client only renders what came back.
    /// </summary>
    public partial class CompanyVacanciesViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IApiClient _api;
        private Guid _companyId;

        public CompanyVacanciesViewModel(IApiClient api) => _api = api;

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

        [ObservableProperty]
        private string _companyName = string.Empty;

        public ObservableCollection<EmployerVacancyRowModel> Items { get; } = [];

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("companyId", out var value)
                && Guid.TryParse(value?.ToString(), out var id))
            {
                _companyId = id;
            }

            if (query.TryGetValue("companyName", out var name))
                CompanyName = Uri.UnescapeDataString(name?.ToString() ?? string.Empty);
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy || _companyId == Guid.Empty)
                return;

            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                var vacancies = await _api.GetCompanyVacanciesAsync(_companyId);
                Items.Clear();
                foreach (var vacancy in vacancies)
                {
                    Items.Add(new EmployerVacancyRowModel(
                        vacancy.Id,
                        vacancy.Title,
                        vacancy.Status,
                        EnumDisplay.VacancyStatus(vacancy.Status),
                        BuildMetaLine(vacancy),
                        string.Format(AppStrings.ApplicationsCountFormat, vacancy.ApplicationCount),
                        vacancy.Status is "Draft" or "Paused",
                        vacancy.Status is "Active",
                        vacancy.Status is "Draft" or "Active" or "Paused"));
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
        private Task ActivateAsync(EmployerVacancyRowModel? row) => ChangeStatusAsync(row, "Active");

        [RelayCommand]
        private Task PauseAsync(EmployerVacancyRowModel? row) => ChangeStatusAsync(row, "Paused");

        [RelayCommand]
        private Task CloseAsync(EmployerVacancyRowModel? row) => ChangeStatusAsync(row, "Closed");

        private async Task ChangeStatusAsync(EmployerVacancyRowModel? row, string target)
        {
            if (row is null || IsBusy)
                return;

            HasError = false;
            ErrorMessage = null;
            try
            {
                await _api.ChangeVacancyStatusAsync(_companyId, row.Id, target);
                await LoadAfterActionAsync();
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
                    ApiClient.Codes.CompanyNotVerified => AppStrings.ErrorCompanyNotVerified,
                    _ => exception.IsOffline ? AppStrings.ErrorOffline : AppStrings.ErrorGeneric
                };
            }
        }

        [RelayCommand]
        private async Task OpenApplicationsAsync(EmployerVacancyRowModel? row)
        {
            if (row is null)
                return;
            await Shell.Current.GoToAsync(
                $"{AppRoutes.Absolute(AppRoutes.VacancyApplications)}" +
                $"?companyId={_companyId}&vacancyId={row.Id}&title={Uri.EscapeDataString(row.Title)}" +
                $"&companyName={Uri.EscapeDataString(CompanyName)}");
        }

        [RelayCommand]
        private async Task NewVacancyAsync() =>
            await Shell.Current.GoToAsync(
                $"{AppRoutes.Absolute(AppRoutes.VacancyEdit)}?companyId={_companyId}" +
                $"&companyName={Uri.EscapeDataString(CompanyName)}");

        private async Task LoadAfterActionAsync()
        {
            IsBusy = false; // LoadAsync guards on IsBusy; allow the refresh through
            await LoadAsync();
        }

        private static string BuildMetaLine(CompanyVacancySummaryDto vacancy)
        {
            var location = string.IsNullOrWhiteSpace(vacancy.Area)
                ? EnumDisplay.Emirate(vacancy.Emirate)
                : $"{EnumDisplay.Emirate(vacancy.Emirate)} · {vacancy.Area}";
            var salary = vacancy.SalaryMinAed is not null && vacancy.SalaryMaxAed is not null
                ? string.Format(AppStrings.FeedCardSalaryFormat, vacancy.SalaryMinAed, vacancy.SalaryMaxAed)
                : AppStrings.FeedCardSalaryHidden;
            return $"{location} · {salary} · {string.Format(AppStrings.HeadcountFormat, vacancy.Headcount)}";
        }

        /// <summary>Explicit back affordance: pages hide the Shell nav bar, so each
        /// screen carries its own parent route (absolute, like all app navigation).</summary>
        [RelayCommand]
        private Task BackAsync() => Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.EmployerHome));
    }
}
