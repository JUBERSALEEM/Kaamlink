using KaamLink.Application.Abstractions;
using KaamLink.Domain.Jobs;
using KaamLink.Domain.Shared;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Jobs;

public sealed class VacancyRepository(AppDbContext db) : IVacancyRepository
{
    public async Task AddAsync(Vacancy vacancy, CancellationToken ct = default) =>
        await db.Vacancies.AddAsync(vacancy, ct);

    public Task<Vacancy?> FindByIdAsync(Guid vacancyId, CancellationToken ct = default) =>
        db.Vacancies.FirstOrDefaultAsync(v => v.Id == vacancyId, ct);

    public async Task<IReadOnlyList<Vacancy>> SearchActiveAsync(
        Emirate? emirate, Guid? skillId, string? keyword, int skip, int take, CancellationToken ct = default)
    {
        var query = db.Vacancies
            .AsNoTracking()
            .Where(v => v.Status == VacancyStatus.Active);

        if (emirate is not null)
            query = query.Where(v => v.Emirate == emirate);
        if (skillId is not null)
            query = query.Where(v => v.Skills.Any(s => s.SkillId == skillId.Value));
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(v => EF.Functions.ILike(v.Title, $"%{keyword.Trim()}%"));

        return await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Vacancy>> ListByCompanyAsync(Guid companyId, CancellationToken ct = default) =>
        await db.Vacancies
            .AsNoTracking()
            .Where(v => v.CompanyId == companyId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);

    public async Task ReplaceSkillsAsync(
        Guid vacancyId, IReadOnlyList<VacancySkill> skills, CancellationToken ct = default)
    {
        var existing = await db.VacancySkills
            .Where(s => s.VacancyId == vacancyId)
            .ToListAsync(ct);

        db.VacancySkills.RemoveRange(existing);

        if (skills.Count > 0)
            await db.VacancySkills.AddRangeAsync(skills, ct);
    }

    public async Task<IReadOnlyList<VacancySkill>> ListSkillsAsync(Guid vacancyId, CancellationToken ct = default) =>
        await db.VacancySkills
            .Where(s => s.VacancyId == vacancyId)
            .AsNoTracking()
            .ToListAsync(ct);
}
