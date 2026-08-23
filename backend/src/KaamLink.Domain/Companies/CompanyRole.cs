namespace KaamLink.Domain.Companies;

/// <summary>
/// Company membership role (ADR 0003). Server-side authorization checks consult this;
/// the UI may hide actions but never enforces (Master Prompt §14/§38).
/// </summary>
public enum CompanyRole
{
    Owner,
    Admin,
    HiringManager,
    Supervisor,
    Viewer
}
