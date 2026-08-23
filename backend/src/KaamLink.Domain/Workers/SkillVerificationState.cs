namespace KaamLink.Domain.Workers;

/// <summary>
/// Trust state of a skill claim (Skill Passport, Master Prompt §17).
/// Nothing may ever be marked verified without an actual verification record —
/// new claims always start as SelfDeclared.
/// </summary>
public enum SkillVerificationState
{
    SelfDeclared,
    EmployerVerified,
    AssessmentVerified
}
