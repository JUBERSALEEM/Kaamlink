using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Jobs
{
    /// <summary>One vacancy card in the worker feed (labels pre-localized for binding).</summary>
    public sealed record VacancyCardModel(
        Guid Id,
        string Title,
        string CompanyLine,
        string LocationLine,
        string SalaryText,
        string HeadcountText,
        string CompanyVerificationState,
        IReadOnlyList<string> Skills);

    /// <summary>
    /// Worker job feed: server-driven active vacancies with emirate/keyword filters
    /// and honest paging (the server computes HasMore from a take+1 fetch).
    /// </summary>
    public partial class VacancyFeedViewModel : ObservableObject
    {
        private const int PageSize = 20;

        private static readonly List<EnumOption> EmirateChoices =
            [new("", AppStrings.FeedEmirateAll), .. EnumDisplay.Emirates];

        private readonly IApiClient _api;
        private int _page = 1;

        public VacancyFeedViewModel(IApiClient api) => _api = api;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isLoaded;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _hasMore;

        [ObservableProperty]
        private bool _isEmpty;

        /// <summary>Rows actually loaded; paired with a localized label on the page so
        /// no pluralised format string is needed in either language.</summary>
        [ObservableProperty]
        private int _resultsCount;

        public IReadOnlyList<EnumOption> EmirateOptions => EmirateChoices;

        [ObservableProperty]
        private EnumOption _selectedEmirate = EmirateChoices[0];

        public ObservableCollection<VacancyCardModel> Items { get; } = [];

        [RelayCommand]
        private async Task SearchAsync()
        {
            // Never clear the list while a request is in flight: the in-flight page
            // would repopulate it with results for the previous query.
            if (IsBusy)
                return;

            Items.Clear();
            IsEmpty = false;
            await LoadPageAsync(reset: true);
        }

        [RelayCommand]
        private async Task LoadMoreAsync()
        {
            if (IsBusy || !HasMore)
                return;

            await LoadPageAsync(reset: false);
        }

        /// <summary>
        /// Fetches one page. The page cursor only advances after a successful
        /// response, so a failed "load more" does not silently skip a page of jobs.
        /// </summary>
        private async Task LoadPageAsync(bool reset)
        {
            if (IsBusy)
                return;

            var requestedPage = reset ? 1 : _page + 1;
            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                var result = await _api.SearchVacanciesAsync(
                    string.IsNullOrEmpty(SelectedEmirate.Value) ? null : SelectedEmirate.Value,
                    skillId: null,
                    SearchText,
                    requestedPage,
                    PageSize);

                foreach (var vacancy in result.Items)
                    Items.Add(Map(vacancy));

                _page = requestedPage;      // advance only after the server answered
                HasMore = result.HasMore;
                IsEmpty = Items.Count == 0;
                ResultsCount = Items.Count;
                IsLoaded = true;
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
                    // The feed gates on completed onboarding — say so, don't shrug generically.
                    ApiClient.Codes.OnboardingIncomplete or ApiClient.Codes.ProfileRequired =>
                        AppStrings.ErrorOnboardingIncomplete,
                    _ => exception.IsOffline ? AppStrings.ErrorOffline : AppStrings.ErrorGeneric
                };
                IsEmpty = Items.Count == 0;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task OpenVacancyAsync(VacancyCardModel? card)
        {
            if (card is null)
                return;
            await Shell.Current.GoToAsync(
                $"{AppRoutes.Absolute(AppRoutes.VacancyDetail)}?vacancyId={card.Id}");
        }

        private static VacancyCardModel Map(VacancyFeedItemDto vacancy)
        {
            var salary = vacancy.SalaryMinAed is not null && vacancy.SalaryMaxAed is not null
                ? string.Format(AppStrings.FeedCardSalaryFormat, vacancy.SalaryMinAed, vacancy.SalaryMaxAed)
                : AppStrings.FeedCardSalaryHidden;

            var location = string.IsNullOrWhiteSpace(vacancy.Area)
                ? EnumDisplay.Emirate(vacancy.Emirate)
                : $"{EnumDisplay.Emirate(vacancy.Emirate)} · {vacancy.Area}";

            return new VacancyCardModel(
                vacancy.Id,
                vacancy.Title,
                vacancy.CompanyName,
                location,
                salary,
                string.Format(AppStrings.HeadcountFormat, vacancy.Headcount),
                vacancy.CompanyVerificationState,
                vacancy.Skills.Select(skill => LocalizationHelpers.LocalizedSkill(skill)).ToList());
        }

        /// <summary>Explicit back affordance to the worker home.</summary>
        [RelayCommand]
        private Task BackAsync() => Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.WorkerHome));
    }
}
