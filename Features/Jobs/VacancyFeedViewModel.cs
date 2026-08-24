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
        string SkillsLine);

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

        public IReadOnlyList<EnumOption> EmirateOptions => EmirateChoices;

        [ObservableProperty]
        private EnumOption _selectedEmirate = EmirateChoices[0];

        public ObservableCollection<VacancyCardModel> Items { get; } = [];

        [RelayCommand]
        private async Task SearchAsync()
        {
            _page = 1;
            Items.Clear();
            await LoadPageAsync();
        }

        [RelayCommand]
        private async Task LoadMoreAsync()
        {
            if (!HasMore || IsBusy)
                return;
            _page += 1;
            await LoadPageAsync();
        }

        private async Task LoadPageAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                var result = await _api.SearchVacanciesAsync(
                    string.IsNullOrEmpty(SelectedEmirate.Value) ? null : SelectedEmirate.Value,
                    skillId: null,
                    SearchText,
                    _page,
                    PageSize);

                foreach (var vacancy in result.Items)
                    Items.Add(Map(vacancy));

                HasMore = result.HasMore;
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

            // ✓ = server-confirmed company verification state (never client-assumed).
            var company = vacancy.CompanyVerificationState == "Verified"
                ? vacancy.CompanyName + " ✓"
                : vacancy.CompanyName;

            var location = string.IsNullOrWhiteSpace(vacancy.Area)
                ? EnumDisplay.Emirate(vacancy.Emirate)
                : $"{EnumDisplay.Emirate(vacancy.Emirate)} · {vacancy.Area}";

            return new VacancyCardModel(
                vacancy.Id,
                vacancy.Title,
                company,
                location,
                salary,
                string.Format(AppStrings.HeadcountFormat, vacancy.Headcount),
                string.Join(" · ", vacancy.Skills.Select(s => LocalizationHelpers.LocalizedSkill(s))));
        }
    }
}
