namespace KaamLink.Application.Workers;

// ---- Responses ----

public sealed record WorkerProfileResponse(
    Guid Id,
    string DisplayName,
    Guid? PrimarySkillId,
    string? Emirate,
    string? Area,
    string? Availability,
    int? ExpectedSalaryMinAed,
    int? ExpectedSalaryMaxAed,
    string? About,
    int ProfileCompletenessPct,
    bool OnboardingComplete,
    IReadOnlyList<WorkerSkillItem> Skills);

public sealed record WorkerSkillItem(
    Guid SkillId,
    string NameEn,
    string NameAr,
    bool RequiresCertification,
    int YearsExperience,
    bool IsPrimary,
    string VerificationState);

// ---- Requests (progressive patch: null = leave unchanged) ----

public sealed record UpdateWorkerProfileRequest
{
    public string? DisplayName { get; init; }

    /// <summary>Presence flag distinguishes "not provided" from "clear" for nullable primary skill.</summary>
    public Guid? PrimarySkillId { get; init; }

    public bool PrimarySkillProvided => PrimarySkillId is not null;

    public string? Emirate { get; init; }

    public string? Area { get; init; }

    public string? Availability { get; init; }

    public int? ExpectedSalaryMinAed { get; init; }

    public int? ExpectedSalaryMaxAed { get; init; }

    public string? About { get; init; }
}

public sealed record SetWorkerSkillsRequest
{
    /// <summary>Full replacement set (empty clears). Sent after the skills onboarding step.</summary>
    public List<SetWorkerSkillItem> Skills { get; init; } = [];
}

public sealed record SetWorkerSkillItem(Guid SkillId, int YearsExperience);
