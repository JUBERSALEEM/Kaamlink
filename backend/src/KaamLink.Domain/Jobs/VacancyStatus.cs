namespace KaamLink.Domain.Jobs;

/// <summary>
/// Vacancy lifecycle. Closed is terminal — a closed vacancy never reopens
/// (post a new one instead). Status names are part of the API contract.
/// </summary>
public enum VacancyStatus
{
    Draft,
    Active,
    Paused,
    Closed
}
