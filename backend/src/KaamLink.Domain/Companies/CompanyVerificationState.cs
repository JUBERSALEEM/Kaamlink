namespace KaamLink.Domain.Companies;

/// <summary>
/// Trust state of a company (Master Prompt §16). New companies are Pending and are
/// NEVER displayed as verified without an admin review record. Stored as text.
/// </summary>
public enum CompanyVerificationState
{
    Pending,
    Verified,
    Rejected,
    Suspended,
    Expired
}
