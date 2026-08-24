namespace KaamLink.Domain.Jobs;

/// <summary>
/// A worker's application to a vacancy — the direct worker↔employer link the
/// platform exists for. One per (vacancy, worker), enforced by a unique index.
/// V1 scope (documented): Withdrawn is terminal — no re-apply after withdrawal.
/// </summary>
public sealed class JobApplication
{
    public Guid Id { get; private set; }

    public Guid VacancyId { get; private set; }

    public Guid WorkerProfileId { get; private set; }

    public ApplicationStatus Status { get; private set; }

    /// <summary>Optional short note to the employer (max 500 chars, validated in the service).</summary>
    public string? CoverNote { get; private set; }

    /// <summary>When the employer last changed the status (null = never reviewed).</summary>
    public DateTimeOffset? ReviewedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private JobApplication()
    {
    }

    public static JobApplication Create(
        Guid vacancyId, Guid workerProfileId, string? coverNote, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        VacancyId = vacancyId,
        WorkerProfileId = workerProfileId,
        Status = ApplicationStatus.Applied,
        CoverNote = string.IsNullOrWhiteSpace(coverNote) ? null : coverNote.Trim(),
        CreatedAt = now,
        UpdatedAt = now
    };

    public bool IsTerminal =>
        Status is ApplicationStatus.Rejected or ApplicationStatus.Hired or ApplicationStatus.Withdrawn;

    /// <summary>Employer-side transition; terminal applications are final.</summary>
    public void ApplyReview(ApplicationStatus next, DateTimeOffset now)
    {
        if (IsTerminal)
            throw new InvalidOperationException("A terminal application cannot change status.");
        if (next is ApplicationStatus.Applied or ApplicationStatus.Withdrawn)
            throw new InvalidOperationException("Employers can only view, shortlist, reject or hire.");
        Status = next;
        ReviewedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Worker-side withdrawal while the employer's decision is still open.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        if (IsTerminal)
            throw new InvalidOperationException("A terminal application cannot be withdrawn.");
        Status = ApplicationStatus.Withdrawn;
        UpdatedAt = now;
    }
}
