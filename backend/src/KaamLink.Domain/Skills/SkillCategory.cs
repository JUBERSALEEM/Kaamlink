namespace KaamLink.Domain.Skills;

/// <summary>Reference data: occupation category (seeded, curated by admins).</summary>
public sealed class SkillCategory
{
    public Guid Id { get; private set; }

    public string NameEn { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public List<Skill> Skills { get; private set; } = [];

    private SkillCategory()
    {
    }

    public static SkillCategory Create(Guid id, string nameEn, string nameAr, int sortOrder) => new()
    {
        Id = id,
        NameEn = nameEn,
        NameAr = nameAr,
        SortOrder = sortOrder
    };
}
