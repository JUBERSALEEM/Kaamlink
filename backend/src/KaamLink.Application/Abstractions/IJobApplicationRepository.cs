using KaamLink.Application.Jobs;
using KaamLink.Domain.Jobs;

namespace KaamLink.Application.Abstractions;

public interface IJobApplicationRepository
{
    Task AddAsync(JobApplication application, CancellationToken ct = default);

    Task<JobApplication?> FindByIdAsync(Guid applicationId, CancellationToken ct = default);

    Task<JobApplication?> FindByVacancyAndWorkerAsync(
        Guid vacancyId, Guid workerProfileId, CancellationToken ct = default);

    /// <summary>The worker's own applications, newest first.</summary>
    Task<IReadOnlyList<JobApplication>> ListByWorkerAsync(Guid workerProfileId, CancellationToken ct = default);

    /// <summary>Applications to one vacancy, newest first.</summary>
    Task<IReadOnlyList<JobApplication>> ListByVacancyAsync(Guid vacancyId, CancellationToken ct = default);

    /// <summary>Applicant read-model rows (profile ⊕ user phone) for the employer's list.</summary>
    Task<IReadOnlyList<ApplicantSnapshot>> GetApplicantSnapshotsAsync(
        IReadOnlyCollection<Guid> workerProfileIds, CancellationToken ct = default);

    /// <summary>Application counts per vacancy (employer vacancy list).</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByVacancyAsync(
        IReadOnlyCollection<Guid> vacancyIds, CancellationToken ct = default);
}
