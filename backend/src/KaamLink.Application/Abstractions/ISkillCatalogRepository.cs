using KaamLink.Domain.Skills;

namespace KaamLink.Application.Abstractions;

public interface ISkillCatalogRepository
{
    /// <summary>All categories with their skills, ordered for display.</summary>
    Task<IReadOnlyList<SkillCategory>> ListCategoriesWithSkillsAsync(CancellationToken ct = default);

    Task<Skill?> FindSkillAsync(Guid skillId, CancellationToken ct = default);

    Task<bool> SkillExistsAsync(Guid skillId, CancellationToken ct = default);
}
