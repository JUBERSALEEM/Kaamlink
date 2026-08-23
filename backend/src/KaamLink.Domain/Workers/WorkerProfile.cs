using KaamLink.Domain.Shared;

namespace KaamLink.Domain.Workers;

/// <summary>
/// The worker's professional identity — owned by the worker, independent of any
/// employer (ADR 0003). Progressively completed; education is NOT a field here
/// by design (practical ability over formal education).
/// </summary>
public sealed class WorkerProfile
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The worker's main trade. Referenced skills list is seeded reference data.</summary>
    public Guid? PrimarySkillId { get; private set; }

    public Emirate? Emirate { get; private set; }

    public string? Area { get; private set; }

    public Availability? Availability { get; private set; }

    public int? ExpectedSalaryMinAed { get; private set; }

    public int? ExpectedSalaryMaxAed { get; private set; }

    public string? About { get; private set; }

    public List<WorkerSkill> Skills { get; private set; } = [];

    public int ProfileCompletenessPct { get; private set; }

    public bool OnboardingComplete { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private WorkerProfile()
    {
    }

    public static WorkerProfile Create(Guid userId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CreatedAt = now,
        UpdatedAt = now
    };

    /// <summary>Progressive onboarding patch: only provided (non-null) values change.</summary>
    public void ApplyBasics(
        string? displayName,
        Guid? primarySkillId,
        bool primarySkillProvided,
        Emirate? emirate,
        string? area,
        string? about,
        DateTimeOffset now)
    {
        if (displayName is not null)
            DisplayName = displayName.Trim();
        if (primarySkillProvided)
            PrimarySkillId = primarySkillId;
        if (emirate is not null)
            Emirate = emirate;
        if (area is not null)
            Area = string.IsNullOrWhiteSpace(area) ? null : area.Trim();
        if (about is not null)
            About = string.IsNullOrWhiteSpace(about) ? null : about.Trim();
        UpdatedAt = now;
    }

    public void ApplyAvailability(
        Availability? availability, int? salaryMinAed, int? salaryMaxAed, DateTimeOffset now)
    {
        if (availability is not null)
            Availability = availability;
        if (salaryMinAed is not null)
            ExpectedSalaryMinAed = salaryMinAed;
        if (salaryMaxAed is not null)
            ExpectedSalaryMaxAed = salaryMaxAed;
        UpdatedAt = now;
    }

    /// <summary>
    /// Completeness is computed server-side and persisted — clients render the stored
    /// value, they never invent their own definition of "complete".
    /// </summary>
    public void RecalculateProgress(bool hasAnySkill, DateTimeOffset now)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(DisplayName)) score += 20;
        if (PrimarySkillId is not null) score += 20;
        if (Emirate is not null) score += 15;
        if (Availability is not null) score += 15;
        if (hasAnySkill) score += 15;
        if (ExpectedSalaryMinAed is not null && ExpectedSalaryMaxAed is not null) score += 10;
        if (!string.IsNullOrWhiteSpace(About)) score += 5;

        ProfileCompletenessPct = score;
        OnboardingComplete =
            !string.IsNullOrWhiteSpace(DisplayName)
            && PrimarySkillId is not null
            && Emirate is not null
            && Availability is not null
            && hasAnySkill;
        UpdatedAt = now;
    }
}
