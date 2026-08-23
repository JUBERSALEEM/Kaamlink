namespace KaamLink.Application.Workers;

public interface IWorkerProfileService
{
    Task<WorkerProfileResponse> GetMineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Progressive onboarding patch; creates the profile on first save.</summary>
    Task<WorkerProfileResponse> UpdateProfileAsync(Guid userId, UpdateWorkerProfileRequest request, CancellationToken ct = default);

    /// <summary>Full replacement of the worker's self-declared skill set.</summary>
    Task<WorkerProfileResponse> SetSkillsAsync(Guid userId, SetWorkerSkillsRequest request, CancellationToken ct = default);
}
