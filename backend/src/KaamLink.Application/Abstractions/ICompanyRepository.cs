using KaamLink.Domain.Companies;

namespace KaamLink.Application.Abstractions;

public interface ICompanyRepository
{
    Task AddCompanyAsync(Company company, CancellationToken ct = default);

    Task AddMemberAsync(CompanyMember member, CancellationToken ct = default);

    Task<Company?> FindByIdAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>Every membership of the user, with the company loaded.</summary>
    Task<IReadOnlyList<CompanyMember>> FindMembershipsByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<CompanyMember?> FindMembershipAsync(Guid companyId, Guid userId, CancellationToken ct = default);

    /// <summary>Companies awaiting verification review (admin-lite queue), oldest first.</summary>
    Task<IReadOnlyList<Company>> ListPendingReviewAsync(CancellationToken ct = default);

    Task<int> CountOwnedCompaniesAsync(Guid userId, CancellationToken ct = default);
}
