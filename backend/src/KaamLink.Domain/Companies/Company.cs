using KaamLink.Domain.Shared;

namespace KaamLink.Domain.Companies;

/// <summary>
/// A hiring organization — the legal/commercial entity, not a person. Verification
/// state changes only via platform-admin review (admin-lite endpoint in V1).
/// </summary>
public sealed class Company
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public BusinessCategory Category { get; private set; }

    public Emirate Emirate { get; private set; }

    public string? Area { get; private set; }

    public string? TradeLicenseNumber { get; private set; }

    public string? Description { get; private set; }

    public CompanyVerificationState VerificationState { get; private set; }

    /// <summary>Reason recorded on rejection (required then; shown to the company).</summary>
    public string? VerificationReason { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private Company()
    {
    }

    public static Company Create(
        string name,
        BusinessCategory category,
        Emirate emirate,
        string? area,
        string? tradeLicenseNumber,
        string? description,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        Category = category,
        Emirate = emirate,
        Area = string.IsNullOrWhiteSpace(area) ? null : area.Trim(),
        TradeLicenseNumber = string.IsNullOrWhiteSpace(tradeLicenseNumber) ? null : tradeLicenseNumber.Trim(),
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        VerificationState = CompanyVerificationState.Pending,
        CreatedAt = now,
        UpdatedAt = now
    };

    public bool IsPendingReview => VerificationState == CompanyVerificationState.Pending;

    /// <summary>Admin decision. Only Pending companies can be reviewed; rejection requires a reason.</summary>
    public void ApplyReview(bool approve, string? reason, DateTimeOffset now)
    {
        if (!IsPendingReview)
            throw new InvalidOperationException("Only pending companies can be reviewed.");

        if (approve)
        {
            VerificationState = CompanyVerificationState.Verified;
            VerifiedAt = now;
            VerificationReason = null;
        }
        else
        {
            VerificationState = CompanyVerificationState.Rejected;
            VerificationReason = reason?.Trim();
        }

        UpdatedAt = now;
    }
}
