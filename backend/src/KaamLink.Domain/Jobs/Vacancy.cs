using KaamLink.Domain.Shared;

namespace KaamLink.Domain.Jobs;

/// <summary>
/// A direct-hire job posting by a company — no intermediaries. Always starts as
/// Draft; activation rules (company verification) live in the application layer.
/// Status transitions are guarded by <see cref="CanTransitionTo"/>/<see cref="ChangeStatus"/>.
/// </summary>
public sealed class Vacancy
{
    public const int MaxSkills = 10;

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Emirate Emirate { get; private set; }

    public string? Area { get; private set; }

    public int? SalaryMinAed { get; private set; }

    public int? SalaryMaxAed { get; private set; }

    public int Headcount { get; private set; }

    public VacancyStatus Status { get; private set; }

    /// <summary>Company member who created the vacancy (accountability).</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Required trades (1..10), sourced from the seeded occupation catalog.</summary>
    public List<VacancySkill> Skills { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private Vacancy()
    {
    }

    public static Vacancy Create(
        Guid companyId, Guid createdByUserId, Emirate emirate, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        CreatedByUserId = createdByUserId,
        Emirate = emirate,
        Headcount = 1,
        Status = VacancyStatus.Draft,
        CreatedAt = now,
        UpdatedAt = now
    };

    /// <summary>Full-content replace (create + edit share one invariant path).</summary>
    public void ApplyContent(
        string title,
        string? description,
        Emirate emirate,
        string? area,
        int? salaryMinAed,
        int? salaryMaxAed,
        int headcount,
        DateTimeOffset now)
    {
        if (Status is VacancyStatus.Closed)
            throw new InvalidOperationException("Closed vacancies cannot be edited.");

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Emirate = emirate;
        Area = string.IsNullOrWhiteSpace(area) ? null : area.Trim();
        SalaryMinAed = salaryMinAed;
        SalaryMaxAed = salaryMaxAed;
        Headcount = headcount;
        UpdatedAt = now;
    }

    /// <summary>Legal transitions: Draft→Active/Closed, Active↔Paused, Active/Paused→Closed.</summary>
    public bool CanTransitionTo(VacancyStatus next) => (Status, next) switch
    {
        (VacancyStatus.Draft, VacancyStatus.Active) => true,
        (VacancyStatus.Draft, VacancyStatus.Closed) => true,
        (VacancyStatus.Active, VacancyStatus.Paused) => true,
        (VacancyStatus.Active, VacancyStatus.Closed) => true,
        (VacancyStatus.Paused, VacancyStatus.Active) => true,
        (VacancyStatus.Paused, VacancyStatus.Closed) => true,
        _ => false
    };

    public void ChangeStatus(VacancyStatus next, DateTimeOffset now)
    {
        if (!CanTransitionTo(next))
            throw new InvalidOperationException($"Cannot move a vacancy from {Status} to {next}.");
        Status = next;
        UpdatedAt = now;
    }
}
