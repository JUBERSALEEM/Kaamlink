using KaamLink.Application.Abstractions;
using KaamLink.Domain.Skills;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Skills;

public sealed class SkillCatalogRepository(AppDbContext db) : ISkillCatalogRepository
{
    public async Task<IReadOnlyList<SkillCategory>> ListCategoriesWithSkillsAsync(CancellationToken ct = default) =>
        await db.SkillCategories
            .Include(c => c.Skills.OrderBy(s => s.SortOrder))
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

    public Task<Skill?> FindSkillAsync(Guid skillId, CancellationToken ct = default) =>
        db.Skills.FirstOrDefaultAsync(s => s.Id == skillId, ct);

    public Task<bool> SkillExistsAsync(Guid skillId, CancellationToken ct = default) =>
        db.Skills.AnyAsync(s => s.Id == skillId, ct);
}
