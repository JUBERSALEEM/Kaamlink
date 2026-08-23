using KaamLink.Domain.Workers;

namespace KaamLink.Application.Abstractions;

public interface IWorkerProfileRepository
{
    Task<WorkerProfile?> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(WorkerProfile profile, CancellationToken ct = default);

    /// <summary>Full replace of the worker's skill list (idempotent per call).</summary>
    Task ReplaceSkillsAsync(Guid workerProfileId, IReadOnlyList<WorkerSkill> skills, CancellationToken ct = default);

    /// <summary>Current skills of the profile (fresh, for response mapping).</summary>
    Task<IReadOnlyList<WorkerSkill>> ListSkillsAsync(Guid workerProfileId, CancellationToken ct = default);
}
