using KaamLink.Domain.Skills;

namespace KaamLink.Domain.Assessments;

/// <summary>
/// The MCQ assessment template for ONE skill — at most one active template per
/// skill in V1 (enforced by a unique index). The pass mark is a percentage of
/// correct answers. Question banks are curated server-side; correct answers never
/// leave the API (the read contract excludes them by construction, §50).
/// </summary>
public sealed class SkillAssessment
{
    public Guid Id { get; private set; }

    public Guid SkillId { get; private set; }

    public Skill Skill { get; private set; } = default!;

    public int PassMarkPercent { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private SkillAssessment()
    {
    }

    public static SkillAssessment Create(
        Guid id, Guid skillId, int passMarkPercent, DateTimeOffset now)
    {
        if (passMarkPercent is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(passMarkPercent));

        return new SkillAssessment
        {
            Id = id,
            SkillId = skillId,
            PassMarkPercent = passMarkPercent,
            IsActive = true,
            CreatedAt = now
        };
    }
}
