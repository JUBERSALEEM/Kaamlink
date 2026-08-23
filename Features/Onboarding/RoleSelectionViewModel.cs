namespace KaamLink.Features.Onboarding
{
    /// <summary>
    /// Entry-point choice between the Worker and Employer experiences.
    /// Worker and Employer are separate product experiences with separate
    /// navigation roots — they share only infrastructure (see docs/phase-01-report.md, section D).
    /// </summary>
    public partial class RoleSelectionViewModel : ObservableObject
    {
        [RelayCommand]
        private Task NavigateToWorkerAsync() => Shell.Current.GoToAsync(AppRoutes.WorkerHome);

        [RelayCommand]
        private Task NavigateToEmployerAsync() => Shell.Current.GoToAsync(AppRoutes.EmployerHome);
    }
}
