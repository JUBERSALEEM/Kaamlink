namespace KaamLink.Application.Jobs;

public interface IJobApplicationService
{
    /// <summary>Worker applies to an active vacancy (requires completed onboarding; one per vacancy).</summary>
    Task<ApplicationItemResponse> ApplyAsync(
        Guid userId, Guid vacancyId, ApplyRequest request, CancellationToken ct = default);

    /// <summary>The worker's own applications (empty when no profile exists yet).</summary>
    Task<IReadOnlyList<ApplicationItemResponse>> ListMineAsync(Guid userId, CancellationToken ct = default);

    Task<ApplicationItemResponse> WithdrawAsync(Guid userId, Guid applicationId, CancellationToken ct = default);

    /// <summary>Employer: applicants of one vacancy (hiring roles only — contains phone numbers).</summary>
    Task<IReadOnlyList<VacancyApplicationItemResponse>> ListForVacancyAsync(
        Guid userId, Guid companyId, Guid vacancyId, CancellationToken ct = default);

    /// <summary>Employer: view / shortlist / reject / hire (terminal states are final).</summary>
    Task<ApplicationStatusChangeResponse> ChangeStatusAsync(
        Guid userId, Guid companyId, Guid vacancyId, Guid applicationId,
        ChangeApplicationStatusRequest request, CancellationToken ct = default);
}
