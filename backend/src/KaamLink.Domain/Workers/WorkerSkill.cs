using KaamLink.Domain.Skills;

namespace KaamLink.Domain.Workers;

/// <summary>
/// A skill on a worker's Skill Passport. Always starts SelfDeclared; employer/assessment
/// verification is written later by the assessment workflows (Phase 05+), never by the worker.
/// </summary>
public sealed class WorkerSkill
{
    public Guid WorkerProfileId { get; private set; }

    public WorkerProfile Profile { get; private set; } = default!;

    public Guid SkillId { get; private set; }

    public Skill Skill { get; private set; } = default!;

    public int YearsExperience { get; private set; }

    /// <summary>Exactly one entry matches the profile's primary trade.</summary>
    public bool IsPrimary { get; private set; }

    public SkillVerificationState VerificationState { get; private set; }

    /// <summary>Timestamp of the assessment pass that earned AssessmentVerified (Phase 05).</summary>
    public DateTimeOffset? AssessmentVerifiedAt { get; private set; }

    private WorkerSkill()
    {
    }

    public static WorkerSkill Create(Guid workerProfileId, Guid skillId, int yearsExperience, bool isPrimary) => new()
    {
        WorkerProfileId = workerProfileId,
        SkillId = skillId,
        YearsExperience = yearsExperience,
        IsPrimary = isPrimary,
        VerificationState = SkillVerificationState.SelfDeclared
    };

    /// <summary>Written ONLY by the assessment service after a server-scored pass — never by the worker.</summary>
    public void MarkAssessmentVerified(DateTimeOffset now)
    {
        VerificationState = SkillVerificationState.AssessmentVerified;
        AssessmentVerifiedAt = now;
    }
}
