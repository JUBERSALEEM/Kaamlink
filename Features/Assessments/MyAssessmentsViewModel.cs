using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Assessments
{
    /// <summary>One row in "My assessments" (worker): a claimed skill and its trust state.</summary>
    public sealed record AssessmentRowModel(
        Guid SkillId,
        string SkillName,
        string StateRaw,
        string StateLabel,
        string ScoreLine,
        bool HasScore,
        bool Available);

    /// <summary>
    /// "My assessments": the worker's claimed skills with assessment availability,
    /// verification state and latest score. States and scores come only from the
    /// server — a pass is earned, never assumed (§50).
    /// </summary>
    public partial class MyAssessmentsViewModel : ObservableObject
    {
        private readonly IApiClient _api;

        public MyAssessmentsViewModel(IApiClient api) => _api = api;

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

        public ObservableCollection<AssessmentRowModel> Items { get; } = [];

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
                var items = await _api.GetMyAssessmentsAsync();
                Items.Clear();
                foreach (var item in items)
                    Items.Add(Map(item));

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
                ErrorMessage = exception.Code switch
                {
                    ApiClient.Codes.OnboardingIncomplete or ApiClient.Codes.ProfileRequired =>
                        AppStrings.ErrorOnboardingIncomplete,
                    _ => exception.IsOffline ? AppStrings.ErrorOffline : AppStrings.ErrorGeneric
                };
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task OpenAssessmentAsync(AssessmentRowModel? row)
        {
            if (row is null || !row.Available)
                return;

            await Shell.Current.GoToAsync(
                $"{AppRoutes.Absolute(AppRoutes.AssessmentRun)}?skillId={row.SkillId}&skillName={Uri.EscapeDataString(row.SkillName)}");
        }

        private static AssessmentRowModel Map(MyAssessmentItemDto item)
        {
            var stateLabel = item.VerificationState switch
            {
                "AssessmentVerified" => AppStrings.StateAssessmentVerified,
                "EmployerVerified" => AppStrings.StateEmployerVerified,
                _ => AppStrings.StateSelfDeclared
            };

            var hasScore = item.LatestScorePercent is not null;
            var scoreLine = hasScore
                ? string.Format(AppStrings.AssessmentScoreFormat, item.LatestScorePercent!.Value)
                    + " · "
                    + (item.LatestPassed == true
                        ? AppStrings.AssessmentPassedShort
                        : AppStrings.AssessmentFailedShort)
                : string.Empty;

            return new AssessmentRowModel(
                item.SkillId,
                item.LocalizedSkillName,
                item.VerificationState,
                stateLabel,
                scoreLine,
                hasScore,
                item.AssessmentAvailable);
        }

        /// <summary>Explicit back affordance: pages hide the Shell nav bar, so each
        /// screen carries its own parent route (absolute, like all app navigation).</summary>
        [RelayCommand]
        private Task BackAsync() => Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.WorkerHome));
    }
}
