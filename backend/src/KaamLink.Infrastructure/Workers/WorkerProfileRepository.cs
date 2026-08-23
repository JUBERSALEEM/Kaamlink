using KaamLink.Application.Abstractions;
using KaamLink.Domain.Workers;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Workers;

public sealed class WorkerProfileRepository(AppDbContext db) : IWorkerProfileRepository
{
    public Task<WorkerProfile?> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        db.WorkerProfiles.FirstOrDefaultAsync(w => w.UserId == userId, ct);

    public async Task AddAsync(WorkerProfile profile, CancellationToken ct = default) =>
        await db.WorkerProfiles.AddAsync(profile, ct);

    public async Task ReplaceSkillsAsync(
        Guid workerProfileId, IReadOnlyList<WorkerSkill> skills, CancellationToken ct = default)
    {
        var existing = await db.WorkerSkills
            .Where(s => s.WorkerProfileId == workerProfileId)
            .ToListAsync(ct);

        db.WorkerSkills.RemoveRange(existing);

        if (skills.Count > 0)
            await db.WorkerSkills.AddRangeAsync(skills, ct);
    }

    public async Task<IReadOnlyList<WorkerSkill>> ListSkillsAsync(Guid workerProfileId, CancellationToken ct = default) =>
        await db.WorkerSkills
            .Where(s => s.WorkerProfileId == workerProfileId)
            .AsNoTracking()
            .ToListAsync(ct);
}
