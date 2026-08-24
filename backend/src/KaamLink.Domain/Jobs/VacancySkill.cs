using KaamLink.Domain.Skills;

namespace KaamLink.Domain.Jobs;

/// <summary>
/// A required trade on a vacancy (composite key). References the seeded occupation
/// catalog — a vacancy never invents free-text professions.
/// </summary>
public sealed class VacancySkill
{
    public Guid VacancyId { get; private set; }

    public Vacancy Vacancy { get; private set; } = default!;

    public Guid SkillId { get; private set; }

    public Skill Skill { get; private set; } = default!;

    private VacancySkill()
    {
    }

    public static VacancySkill Create(Guid vacancyId, Guid skillId) => new()
    {
        VacancyId = vacancyId,
        SkillId = skillId
    };
}
