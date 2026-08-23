namespace KaamLink.Features.Employer
{
    /// <summary>
    /// Employer experience root. Intentionally minimal — vacancies, candidates
    /// and the hiring pipeline are delivered in later phases (see
    /// docs/phase-01-report.md, section P). Exists now so navigation, DI and
    /// the MVVM pattern are established before features arrive.
    /// </summary>
    public partial class EmployerHomeViewModel : ObservableObject
    {
    }
}
