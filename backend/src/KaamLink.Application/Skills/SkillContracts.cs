namespace KaamLink.Application.Skills;

public sealed record SkillCatalogResponse(IReadOnlyList<SkillCategoryItem> Categories);

public sealed record SkillCategoryItem(
    Guid Id,
    string NameEn,
    string NameAr,
    IReadOnlyList<SkillItem> Skills);

public sealed record SkillItem(
    Guid Id,
    string NameEn,
    string NameAr,
    bool RequiresCertification);
