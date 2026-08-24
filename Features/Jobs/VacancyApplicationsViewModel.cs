using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Jobs
{
    /// <summary>One applicant row on the employer's decision list.</summary>
    public sealed record ApplicantRowModel(
        Guid Id,
        string WorkerDisplayName,
        string WorkerPhoneNumber,
        string MetaLine,
        string? CoverNote,
        bool HasCoverNote,
        string Status,
        string StatusLabel,
        bool CanMarkViewed,
        bool CanDecide);

    /// <summary>
    /// Applicants of one vacancy (?companyId=&amp;vacancyId=&amp;title=) with the
    /// employer decision actions. Terminal rows render read-only; the server
    /// enforces the same wall.
    /// </summary>
    public partial class VacancyApplicationsViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IApiClient _api;
        private Guid _companyId;
        private Guid _vacancyId;

        public VacancyApplicationsViewModel(IApiClient api) => _api = api;

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
        private string _vacancyTitle = string.Empty;

        public ObservableCollection<ApplicantRowModel> Items { get; } = [];

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

            if (query.TryGetValue("title", out var titleValue))
                VacancyTitle = Uri.UnescapeDataString(titleValue?.ToString() ?? string.Empty);
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy || _companyId == Guid.Empty || _vacancyId == Guid.Empty)
                return;

            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                var applicants = await _api.GetVacancyApplicationsAsync(_companyId, _vacancyId);
                Items.Clear();
                foreach (var applicant in applicants)
                {
                    var meta = new List<string>();
                    if (!string.IsNullOrWhiteSpace(applicant.WorkerEmirate))
                        meta.Add(EnumDisplay.Emirate(applicant.WorkerEmirate));
                    meta.Add(string.Format(
                        AppStrings.CompletenessFormat, applicant.WorkerProfileCompletenessPct));

                    var isTerminal = applicant.Status is "Rejected" or "Hired" or "Withdrawn";
                    Items.Add(new ApplicantRowModel(
                        applicant.Id,
                        applicant.WorkerDisplayName,
                        applicant.WorkerPhoneNumber,
                        string.Join(" · ", meta),
                        applicant.CoverNote,
                        !string.IsNullOrWhiteSpace(applicant.CoverNote),
                        applicant.Status,
                        EnumDisplay.ApplicationStatus(applicant.Status),
                        applicant.Status == "Applied",
                        !isTerminal));
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
        private Task MarkViewedAsync(ApplicantRowModel? row) => DecideAsync(row, "Viewed");

        [RelayCommand]
        private Task ShortlistAsync(ApplicantRowModel? row) => DecideAsync(row, "Shortlisted");

        [RelayCommand]
        private Task RejectAsync(ApplicantRowModel? row) => DecideAsync(row, "Rejected");

        [RelayCommand]
        private Task HireAsync(ApplicantRowModel? row) => DecideAsync(row, "Hired");

        private async Task DecideAsync(ApplicantRowModel? row, string target)
        {
            if (row is null || IsBusy)
                return;

            HasError = false;
            ErrorMessage = null;
            try
            {
                await _api.ChangeApplicationStatusAsync(_companyId, _vacancyId, row.Id, target);
                IsBusy = false; // allow the refresh through LoadAsync's guard
                await LoadAsync();
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
    }
}
