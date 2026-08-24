namespace KaamLink.Domain.Jobs;

/// <summary>
/// Application lifecycle. Terminal states (Rejected / Hired / Withdrawn) never
/// change again — re-hiring flows create new vacancies, not reopened applications.
/// Status names are part of the API contract.
/// </summary>
public enum ApplicationStatus
{
    Applied,
    Viewed,
    Shortlisted,
    Rejected,
    Hired,
    Withdrawn
}
