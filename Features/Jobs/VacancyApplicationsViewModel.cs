using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
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
        bool CanDecide)
    {
        /// <summary>Up to two initials for the avatar tile (display only, no server data).</summary>
        public string Initials
        {
            get
            {
                var parts = WorkerDisplayName.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return parts.Length switch
                {
                    0 => "?",
                    1 => parts[0][..1].ToUpperInvariant(),
                    _ => string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant()
                };
            }
        }
    }

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

        /// <summary>Re-entrancy latch for one-shot server mutations (not bound to UI).</summary>
        private bool _isWorking;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _isEmpty;

        [ObservableProperty]
        private string _vacancyTitle = string.Empty;

        [ObservableProperty]
        private string _companyName = string.Empty;

        /// <summary>Row count header, formatted from the server-returned list.</summary>
        [ObservableProperty]
        private string _applicantCountText = string.Empty;

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

            if (query.TryGetValue("companyName", out var companyValue2))
                CompanyName = Uri.UnescapeDataString(companyValue2?.ToString() ?? string.Empty);
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
                ApplicantCountText = string.Format(AppStrings.ApplicationsCountFormat, Items.Count);
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
            // Latched, not IsBusy: a second tap (or a tap on another decision button)
            // must not send a second decision for the same applicant.
            if (row is null || IsBusy || _isWorking)
                return;

            _isWorking = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                await _api.ChangeApplicationStatusAsync(_companyId, _vacancyId, row.Id, target);
                await LoadAsync(); // reload from the server: the list is its truth
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
                _isWorking = false;
            }
        }

        /// <summary>Back to the company's vacancy list (query values carried by this route).</summary>
        [RelayCommand]
        private Task BackAsync()
        {
            var url = $"{AppRoutes.Absolute(AppRoutes.CompanyVacancies)}" +
                      $"?companyId={_companyId}&companyName={Uri.EscapeDataString(CompanyName)}";
            return Shell.Current.GoToAsync(url);
        }
    }
}
