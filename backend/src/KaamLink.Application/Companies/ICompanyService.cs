namespace KaamLink.Application.Companies;

public interface ICompanyService
{
    /// <summary>Create a company (state Pending) and make the caller its Owner.</summary>
    Task<CompanySummaryResponse> CreateAsync(Guid userId, CreateCompanyRequest request, CancellationToken ct = default);

    /// <summary>Companies the caller belongs to, with their role in each.</summary>
    Task<IReadOnlyList<CompanySummaryResponse>> GetMineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Admin-lite review queue: pending companies, oldest first. Platform admin only.</summary>
    Task<IReadOnlyList<CompanySummaryResponse>> ListPendingAsync(CancellationToken ct = default);

    /// <summary>Platform-admin decision on a pending company (verify / reject with reason).</summary>
    Task<CompanySummaryResponse> ReviewAsync(Guid adminUserId, Guid companyId, ReviewCompanyRequest request, CancellationToken ct = default);
}
