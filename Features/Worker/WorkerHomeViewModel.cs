using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Worker
{
    /// <summary>One row in the Skill Passport list on the worker home.</summary>
    public sealed record SkillRowModel(string Label, string YearsSummary, string StateLabel, bool IsPrimary);

    /// <summary>
    /// Worker experience root: the live profile (Skill Passport snapshot) pulled
    /// from the API on every appearance. Job discovery itself lands next phase —
    /// the roadmap note says exactly that instead of pretending (§50).
    /// </summary>
    public partial class WorkerHomeViewModel : ObservableObject
    {
        private readonly IApiClient _api;
        private readonly ISessionService _session;

        public WorkerHomeViewModel(IApiClient api, ISessionService session)
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

        [ObservableProperty]
        private string _displayName = string.Empty;

        /// <summary>Profession · emirate headline line.</summary>
        [ObservableProperty]
        private string _headline = string.Empty;

        [ObservableProperty]
        private string _completenessText = string.Empty;

        [ObservableProperty]
        private double _completenessProgress;

        [ObservableProperty]
        private string _availabilityText = string.Empty;

        [ObservableProperty]
        private string _salaryText = string.Empty;

        [ObservableProperty]
        private bool _hasSalary;

        [ObservableProperty]
        private string _about = string.Empty;

        [ObservableProperty]
        private bool _hasAbout;

        public ObservableCollection<SkillRowModel> Skills { get; } = [];

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
                var profile = await _api.GetMyWorkerProfileAsync();
                if (profile is null)
                {
                    // No profile (anymore): onboarding is the honest destination.
                    await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.WorkerOnboarding));
                    return;
                }

                Apply(profile);
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

        private void Apply(WorkerProfileResponseDto profile)
        {
            DisplayName = profile.DisplayName;

            var primary = profile.Skills.FirstOrDefault(s => s.IsPrimary);
            var profession = primary is null ? string.Empty : LocalizationHelpers.LocalizedSkill(primary);
            var emirate = EnumDisplay.Emirate(profile.Emirate);
            Headline = string.IsNullOrEmpty(profession)
                ? emirate
                : string.IsNullOrEmpty(emirate) ? profession : $"{profession} · {emirate}";

            CompletenessText = string.Format(AppStrings.ProfileCompletenessFormat, profile.ProfileCompletenessPct);
            CompletenessProgress = profile.ProfileCompletenessPct / 100.0;

            AvailabilityText = EnumDisplay.Availability(profile.Availability);

            HasSalary = profile.ExpectedSalaryMinAed is not null && profile.ExpectedSalaryMaxAed is not null;
            SalaryText = HasSalary
                ? string.Format(AppStrings.SalaryExpectationFormat, profile.ExpectedSalaryMinAed, profile.ExpectedSalaryMaxAed)
                : string.Empty;

            About = profile.About ?? string.Empty;
            HasAbout = !string.IsNullOrWhiteSpace(About);

            Skills.Clear();
            foreach (var skill in profile.Skills.OrderByDescending(s => s.IsPrimary).ThenByDescending(s => s.YearsExperience))
            {
                var label = skill.IsPrimary
                    ? "★ " + LocalizationHelpers.LocalizedSkill(skill) // ★ = main profession
                    : LocalizationHelpers.LocalizedSkill(skill);
                Skills.Add(new SkillRowModel(
                    label,
                    $"{skill.YearsExperience} {AppStrings.YearsShort}",
                    // V1 skills are self-declared by design; the badge says so and never lies (§50).
                    AppStrings.SelfDeclaredBadge,
                    skill.IsPrimary));
            }
        }

        [RelayCommand]
        private async Task SwitchToEmployerAsync() => await SwitchExperienceAsync(RoleNavigator.Employer);

        internal async Task SwitchExperienceAsync(string role)
        {
            var user = await _session.ReloadUserAsync() ?? _session.CurrentUser;
            await Shell.Current.GoToAsync(user is not null
                ? RoleNavigator.AbsoluteRouteFor(user, role)
                : RoleNavigator.LoginRouteFor(role));
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            await _session.LogoutAsync();
            await Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.Welcome));
        }
    }
}
