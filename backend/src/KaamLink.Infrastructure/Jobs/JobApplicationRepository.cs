using KaamLink.Application.Abstractions;
using KaamLink.Application.Jobs;
using KaamLink.Domain.Jobs;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Jobs;

public sealed class JobApplicationRepository(AppDbContext db) : IJobApplicationRepository
{
    public async Task AddAsync(JobApplication application, CancellationToken ct = default) =>
        await db.JobApplications.AddAsync(application, ct);

    public Task<JobApplication?> FindByIdAsync(Guid applicationId, CancellationToken ct = default) =>
        db.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);

    public Task<JobApplication?> FindByVacancyAndWorkerAsync(
        Guid vacancyId, Guid workerProfileId, CancellationToken ct = default) =>
        db.JobApplications.FirstOrDefaultAsync(
            a => a.VacancyId == vacancyId && a.WorkerProfileId == workerProfileId, ct);

    public async Task<IReadOnlyList<JobApplication>> ListByWorkerAsync(
        Guid workerProfileId, CancellationToken ct = default) =>
        await db.JobApplications
            .AsNoTracking()
            .Where(a => a.WorkerProfileId == workerProfileId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JobApplication>> ListByVacancyAsync(
        Guid vacancyId, CancellationToken ct = default) =>
        await db.JobApplications
            .AsNoTracking()
            .Where(a => a.VacancyId == vacancyId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ApplicantSnapshot>> GetApplicantSnapshotsAsync(
        IReadOnlyCollection<Guid> workerProfileIds, CancellationToken ct = default)
    {
        // Materialize the join first: enum.ToString() inside a projection is not
        // reliably SQL-translatable with EnumToStringConverter — map in memory instead.
        var rows = await db.WorkerProfiles
            .AsNoTracking()
            .Where(w => workerProfileIds.Contains(w.Id))
            .Join(
                db.Users.AsNoTracking(),
                profile => profile.UserId,
                user => user.Id,
                (profile, user) => new { profile, user })
            .ToListAsync(ct);

        return rows.Select(row => new ApplicantSnapshot(
            row.profile.Id,
            row.user.Id,
            row.profile.DisplayName,
            row.user.PhoneNumber,
            row.profile.Emirate?.ToString(),
            row.profile.PrimarySkillId,
            row.profile.ProfileCompletenessPct)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountByVacancyAsync(
        IReadOnlyCollection<Guid> vacancyIds, CancellationToken ct = default) =>
        await db.JobApplications
            .AsNoTracking()
            .Where(a => vacancyIds.Contains(a.VacancyId))
            .GroupBy(a => a.VacancyId)
            .Select(group => new { VacancyId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.VacancyId, row => row.Count, ct);
}
