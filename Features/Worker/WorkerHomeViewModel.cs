namespace KaamLink.Features.Worker
{
    /// <summary>
    /// Worker experience root. Intentionally minimal — job discovery, profile
    /// and the application pipeline are delivered in later phases (see
    /// docs/phase-01-report.md, section P). Exists now so navigation, DI and
    /// the MVVM pattern are established before features arrive.
    /// </summary>
    public partial class WorkerHomeViewModel : ObservableObject
    {
    }
}
