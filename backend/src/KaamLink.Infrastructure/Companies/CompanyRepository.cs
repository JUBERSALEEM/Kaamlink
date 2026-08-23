using KaamLink.Application.Abstractions;
using KaamLink.Domain.Companies;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Companies;

public sealed class CompanyRepository(AppDbContext db) : ICompanyRepository
{
    public async Task AddCompanyAsync(Company company, CancellationToken ct = default) =>
        await db.Companies.AddAsync(company, ct);

    public async Task AddMemberAsync(CompanyMember member, CancellationToken ct = default) =>
        await db.CompanyMembers.AddAsync(member, ct);

    public Task<Company?> FindByIdAsync(Guid companyId, CancellationToken ct = default) =>
        db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct);

    public async Task<IReadOnlyList<CompanyMember>> FindMembershipsByUserIdAsync(
        Guid userId, CancellationToken ct = default) =>
        await db.CompanyMembers
            .Include(m => m.Company)
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

    public Task<CompanyMember?> FindMembershipAsync(Guid companyId, Guid userId, CancellationToken ct = default) =>
        db.CompanyMembers.FirstOrDefaultAsync(
            m => m.CompanyId == companyId && m.UserId == userId, ct);

    public async Task<IReadOnlyList<Company>> ListPendingReviewAsync(CancellationToken ct = default) =>
        await db.Companies
            .Where(c => c.VerificationState == CompanyVerificationState.Pending)
            .OrderBy(c => c.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

    public Task<int> CountOwnedCompaniesAsync(Guid userId, CancellationToken ct = default) =>
        db.CompanyMembers.CountAsync(
            m => m.UserId == userId && m.Role == CompanyRole.Owner, ct);
}
