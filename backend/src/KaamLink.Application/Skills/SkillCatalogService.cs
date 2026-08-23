using KaamLink.Application.Abstractions;

namespace KaamLink.Application.Skills;

public sealed class SkillCatalogService(ISkillCatalogRepository skillCatalog) : ISkillCatalogService
{
    public async Task<SkillCatalogResponse> GetCatalogAsync(CancellationToken ct = default)
    {
        var categories = await skillCatalog.ListCategoriesWithSkillsAsync(ct);

        return new SkillCatalogResponse(
            categories
                .Select(c => new SkillCategoryItem(
                    c.Id,
                    c.NameEn,
                    c.NameAr,
                    c.Skills
                        .Select(s => new SkillItem(s.Id, s.NameEn, s.NameAr, s.RequiresCertification))
                        .ToList()))
                .ToList());
    }
}
