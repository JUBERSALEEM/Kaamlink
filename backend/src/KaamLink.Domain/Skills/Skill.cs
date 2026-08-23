namespace KaamLink.Domain.Skills;

/// <summary>
/// Reference data: an occupation/trade. <see cref="RequiresCertification"/> is curated
/// by admins with legal input — the seed sets it only where a licence is unambiguous
/// (vehicle operation); never invent UAE legal requirements (Master Prompt §1/§L).
/// </summary>
public sealed class Skill
{
    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    public SkillCategory Category { get; private set; } = default!;

    public string NameEn { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public bool RequiresCertification { get; private set; }

    public int SortOrder { get; private set; }

    private Skill()
    {
    }

    public static Skill Create(
        Guid id, Guid categoryId, string nameEn, string nameAr, bool requiresCertification, int sortOrder) => new()
    {
        Id = id,
        CategoryId = categoryId,
        NameEn = nameEn,
        NameAr = nameAr,
        RequiresCertification = requiresCertification,
        SortOrder = sortOrder
    };
}
