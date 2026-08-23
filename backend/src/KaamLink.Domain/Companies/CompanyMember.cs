namespace KaamLink.Domain.Companies;

/// <summary>User ↔ Company membership with a role (composite key). Creator becomes Owner.</summary>
public sealed class CompanyMember
{
    public Guid CompanyId { get; private set; }

    public Company Company { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public CompanyRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private CompanyMember()
    {
    }

    public static CompanyMember Create(
        Guid companyId, Guid userId, CompanyRole role, DateTimeOffset now, Company? company = null) => new()
    {
        CompanyId = companyId,
        UserId = userId,
        Role = role,
        CreatedAt = now,
        Company = company!
    };
}
