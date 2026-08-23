using KaamLink.Application.Abstractions;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;
using KaamLink.Domain.Companies;
using KaamLink.Domain.Shared;

namespace KaamLink.Application.Companies;

/// <summary>
/// Company onboarding + admin-lite verification. Business rule anchors:
/// new companies are Pending and are never presented as verified without a review
/// record (§16); creators become Owner (§14); membership scoping happens in every query.
/// </summary>
public sealed class CompanyService(
    ICompanyRepository companies,
    IUnitOfWork unitOfWork,
    IAuditService audit,
    TimeProvider timeProvider) : ICompanyService
{
    private const int MaxOwnedCompaniesPerUser = 3;

    public async Task<CompanySummaryResponse> CreateAsync(
        Guid userId, CreateCompanyRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 160)
            throw AppException.BadRequest("company.name_invalid", "Company name must be 2–160 characters.");

        var category = ParseCategory(request.Category);
        var emirate = ParseEmirate(request.Emirate);

        if (!string.IsNullOrWhiteSpace(request.TradeLicenseNumber)
            && request.TradeLicenseNumber.Trim().Length > 40)
            throw AppException.BadRequest("company.license_invalid", "Trade licence number is too long.");

        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 2000)
            throw AppException.BadRequest("company.description_invalid", "Description is too long (max 2000 characters).");

        if (await companies.CountOwnedCompaniesAsync(userId, ct) >= MaxOwnedCompaniesPerUser)
            throw AppException.BadRequest("company.too_many", $"You can register at most {MaxOwnedCompaniesPerUser} companies.");

        var company = Company.Create(
            request.Name,
            category,
            emirate,
            request.Area,
            request.TradeLicenseNumber,
            request.Description,
            now);
        await companies.AddCompanyAsync(company, ct);

        var membership = CompanyMember.Create(company.Id, userId, CompanyRole.Owner, now);
        await companies.AddMemberAsync(membership, ct);

        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.CompanyRegistered,
            "company",
            company.Id.ToString(),
            actorUserId: userId,
            ct: ct);

        return ToResponse(company, membership);
    }

    public async Task<IReadOnlyList<CompanySummaryResponse>> GetMineAsync(Guid userId, CancellationToken ct = default) =>
        (await companies.FindMembershipsByUserIdAsync(userId, ct))
            .Select(m => ToResponse(m.Company, m))
            .ToList();

    public async Task<IReadOnlyList<CompanySummaryResponse>> ListPendingAsync(CancellationToken ct = default) =>
        (await companies.ListPendingReviewAsync(ct))
            .Select(c => ToResponse(c, membership: null))
            .ToList();

    public async Task<CompanySummaryResponse> ReviewAsync(
        Guid adminUserId, Guid companyId, ReviewCompanyRequest request, CancellationToken ct = default)
    {
        var company = await companies.FindByIdAsync(companyId, ct)
            ?? throw AppException.NotFound("company.not_found", "Company not found.");

        if (!company.IsPendingReview)
            throw AppException.BadRequest(
                "company.already_reviewed",
                $"Company is already {company.VerificationState.ToString().ToLowerInvariant()} and cannot be reviewed again.");

        if (!request.Approve && string.IsNullOrWhiteSpace(request.Reason))
            throw AppException.BadRequest("company.reason_required", "A reason is required when rejecting a company.");

        company.ApplyReview(request.Approve, request.Reason, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            request.Approve ? AuditActions.CompanyVerified : AuditActions.CompanyRejected,
            "company",
            company.Id.ToString(),
            actorUserId: adminUserId,
            ct: ct);

        return ToResponse(company, membership: null);
    }

    private static CompanySummaryResponse ToResponse(Company company, CompanyMember? membership) => new(
        company.Id,
        company.Name,
        company.Category.ToString(),
        company.Emirate.ToString(),
        company.Area,
        company.TradeLicenseNumber,
        company.VerificationState.ToString(),
        company.VerificationReason,
        company.VerifiedAt,
        membership?.Role.ToString() ?? string.Empty,
        company.CreatedAt);

    private static BusinessCategory ParseCategory(string value) =>
        Enum.TryParse<BusinessCategory>(value, ignoreCase: true, out var category)
            ? category
            : throw AppException.BadRequest("company.category_invalid", "Unknown business category.");

    private static Emirate ParseEmirate(string value) =>
        Enum.TryParse<Emirate>(value, ignoreCase: true, out var emirate)
            ? emirate
            : throw AppException.BadRequest("company.emirate_invalid", "Unknown emirate.");
}
