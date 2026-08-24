using System.Text.Json;
using KaamLink.Application.Abstractions;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;
using KaamLink.Domain.Companies;
using KaamLink.Domain.Jobs;
using KaamLink.Domain.Shared;

namespace KaamLink.Application.Jobs;

/// <summary>
/// Vacancy posting + worker feed. Business rule anchors (product decisions,
/// documented in the Phase 04 report — none of these are legal claims):
///   * writes require membership role Owner/Admin/HiringManager (reads: any member);
///   * publishing (→ Active) requires a Verified company;
///   * workers only ever see Active vacancies (others are 404);
///   * required skills come from the seeded catalog, never free text.
/// </summary>
public sealed class VacancyService(
    IVacancyRepository vacancies,
    ICompanyRepository companies,
    ISkillCatalogRepository skillCatalog,
    IJobApplicationRepository applications,
    IWorkerProfileRepository workerProfiles,
    IUnitOfWork unitOfWork,
    IAuditService audit,
    TimeProvider timeProvider) : IVacancyService
{
    private const int MaxPageSize = 50;

    // ---- worker-facing ----

    public async Task<PagedResponse<VacancyFeedItemResponse>> SearchActiveAsync(
        string? emirate, Guid? skillId, string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
            throw AppException.BadRequest(
                "jobs.paging_invalid",
                $"Page must be at least 1 and page size between 1 and {MaxPageSize}.");

        Emirate? emirateFilter = null;
        if (!string.IsNullOrWhiteSpace(emirate))
        {
            emirateFilter = Enum.TryParse<Emirate>(emirate, ignoreCase: true, out var parsed)
                ? parsed
                : throw AppException.BadRequest("jobs.emirate_invalid", "Unknown emirate.");
        }

        var surplus = await vacancies.SearchActiveAsync(
            emirateFilter, skillId, keyword, (page - 1) * pageSize, pageSize + 1, ct);

        var hasMore = surplus.Count > pageSize;
        var items = new List<VacancyFeedItemResponse>();
        foreach (var vacancy in surplus.Take(pageSize))
            items.Add(await ToFeedItemAsync(vacancy, ct));

        return new PagedResponse<VacancyFeedItemResponse>(items, page, pageSize, hasMore);
    }

    public async Task<VacancyDetailResponse> GetActiveDetailAsync(
        Guid userId, Guid vacancyId, CancellationToken ct = default)
    {
        var vacancy = await vacancies.FindByIdAsync(vacancyId, ct);
        if (vacancy is null || vacancy.Status != VacancyStatus.Active)
            throw AppException.NotFound("jobs.vacancy_not_found", "Vacancy not found.");

        string? ownStatus = null;
        var profile = await workerProfiles.FindByUserIdAsync(userId, ct);
        if (profile is not null)
        {
            var own = await applications.FindByVacancyAndWorkerAsync(vacancyId, profile.Id, ct);
            ownStatus = own?.Status.ToString();
        }

        var company = await companies.FindByIdAsync(vacancy.CompanyId, ct);
        return new VacancyDetailResponse(
            vacancy.Id,
            vacancy.Title,
            vacancy.Description,
            vacancy.CompanyId,
            company?.Name ?? string.Empty,
            company?.VerificationState.ToString() ?? string.Empty,
            vacancy.Emirate.ToString(),
            vacancy.Area,
            vacancy.SalaryMinAed,
            vacancy.SalaryMaxAed,
            vacancy.Headcount,
            await ToSkillItemsAsync(vacancyId, ct),
            ownStatus,
            vacancy.CreatedAt);
    }

    // ---- employer-facing ----

    public async Task<CompanyVacancySummaryResponse> CreateAsync(
        Guid userId, Guid companyId, UpsertVacancyRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        await RequireMembershipAsync(companyId, userId, requireHiringRole: true, ct);

        var emirate = ValidateRequest(request);
        var skills = await ValidateSkillsAsync(request, ct);

        var vacancy = Vacancy.Create(companyId, userId, emirate, now);
        vacancy.ApplyContent(
            request.Title, request.Description, emirate, request.Area,
            request.SalaryMinAed, request.SalaryMaxAed, request.Headcount, now);

        await vacancies.AddAsync(vacancy, ct);
        await vacancies.ReplaceSkillsAsync(
            vacancy.Id,
            skills.Select(skillId => VacancySkill.Create(vacancy.Id, skillId)).ToList(),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.VacancyCreated, "vacancy", vacancy.Id.ToString(), actorUserId: userId, ct: ct);

        return ToSummary(vacancy, applicationCount: 0);
    }

    public async Task<CompanyVacancySummaryResponse> UpdateAsync(
        Guid userId, Guid companyId, Guid vacancyId, UpsertVacancyRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        await RequireMembershipAsync(companyId, userId, requireHiringRole: true, ct);

        var vacancy = await FindOwnedAsync(companyId, vacancyId, ct);
        if (vacancy.Status == VacancyStatus.Closed)
            throw AppException.BadRequest("jobs.vacancy_closed", "Closed vacancies cannot be edited.");

        var emirate = ValidateRequest(request);
        var skills = await ValidateSkillsAsync(request, ct);

        vacancy.ApplyContent(
            request.Title, request.Description, emirate, request.Area,
            request.SalaryMinAed, request.SalaryMaxAed, request.Headcount, now);
        await vacancies.ReplaceSkillsAsync(
            vacancy.Id,
            skills.Select(skillId => VacancySkill.Create(vacancy.Id, skillId)).ToList(),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.VacancyUpdated, "vacancy", vacancy.Id.ToString(), actorUserId: userId, ct: ct);

        return await ToSummaryWithCountAsync(vacancy, ct);
    }

    public async Task<CompanyVacancySummaryResponse> ChangeStatusAsync(
        Guid userId, Guid companyId, Guid vacancyId, ChangeVacancyStatusRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var company = await RequireMembershipAsync(companyId, userId, requireHiringRole: true, ct);

        var vacancy = await FindOwnedAsync(companyId, vacancyId, ct);

        var target = Enum.TryParse<VacancyStatus>(request.Status, ignoreCase: true, out var parsed)
            ? parsed
            : throw AppException.BadRequest("jobs.status_invalid", "Unknown vacancy status.");

        if (!vacancy.CanTransitionTo(target))
            throw AppException.BadRequest(
                "jobs.status_transition_invalid",
                $"Cannot move a vacancy from {vacancy.Status} to {target}.");

        if (target == VacancyStatus.Active && company.VerificationState != CompanyVerificationState.Verified)
            throw AppException.BadRequest(
                "jobs.company_not_verified",
                "The company must be verified before vacancies can be published.");

        var from = vacancy.Status;
        vacancy.ChangeStatus(target, now);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.VacancyStatusChanged,
            "vacancy",
            vacancy.Id.ToString(),
            actorUserId: userId,
            metadataJson: JsonSerializer.Serialize(new
            {
                from = from.ToString(),
                to = target.ToString()
            }),
            ct: ct);

        return await ToSummaryWithCountAsync(vacancy, ct);
    }

    public async Task<IReadOnlyList<CompanyVacancySummaryResponse>> ListCompanyVacanciesAsync(
        Guid userId, Guid companyId, CancellationToken ct = default)
    {
        await RequireMembershipAsync(companyId, userId, requireHiringRole: false, ct);

        var items = await vacancies.ListByCompanyAsync(companyId, ct);
        var counts = await applications.CountByVacancyAsync(items.Select(v => v.Id).ToList(), ct);
        return items
            .Select(v => ToSummary(v, counts.TryGetValue(v.Id, out var count) ? count : 0))
            .ToList();
    }

    // ---- internals ----

    private async Task<Company> RequireMembershipAsync(
        Guid companyId, Guid userId, bool requireHiringRole, CancellationToken ct)
    {
        var membership = await companies.FindMembershipAsync(companyId, userId, ct)
            ?? throw AppException.NotFound("company.not_found", "Company not found.");

        if (requireHiringRole
            && membership.Role is not (CompanyRole.Owner or CompanyRole.Admin or CompanyRole.HiringManager))
        {
            throw AppException.Forbidden("jobs.role_not_allowed", "This membership role cannot manage vacancies.");
        }

        return await companies.FindByIdAsync(companyId, ct)
            ?? throw AppException.NotFound("company.not_found", "Company not found.");
    }

    private async Task<Vacancy> FindOwnedAsync(Guid companyId, Guid vacancyId, CancellationToken ct)
    {
        var vacancy = await vacancies.FindByIdAsync(vacancyId, ct);
        return vacancy is null || vacancy.CompanyId != companyId
            ? throw AppException.NotFound("jobs.vacancy_not_found", "Vacancy not found.")
            : vacancy;
    }

    private static Emirate ValidateRequest(UpsertVacancyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length is < 2 or > 120)
            throw AppException.BadRequest("jobs.title_invalid", "Title must be 2–120 characters.");
        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 4000)
            throw AppException.BadRequest("jobs.description_invalid", "Description is too long (max 4000 characters).");
        if (!string.IsNullOrWhiteSpace(request.Area) && request.Area.Trim().Length > 120)
            throw AppException.BadRequest("jobs.area_invalid", "Area is too long (max 120 characters).");

        var emirate = Enum.TryParse<Emirate>(request.Emirate, ignoreCase: true, out var parsed)
            ? parsed
            : throw AppException.BadRequest("jobs.emirate_invalid", "Unknown emirate.");

        if (request.SalaryMinAed is < 0 or > 1_000_000 || request.SalaryMaxAed is < 0 or > 1_000_000)
            throw AppException.BadRequest("jobs.salary_invalid", "Salary must be between 0 and 1,000,000 AED.");
        if (request.SalaryMinAed is not null && request.SalaryMaxAed is not null
            && request.SalaryMinAed > request.SalaryMaxAed)
            throw AppException.BadRequest("jobs.salary_invalid", "Minimum salary cannot exceed maximum salary.");
        if (request.SalaryMinAed is null != request.SalaryMaxAed is null)
            throw AppException.BadRequest(
                "jobs.salary_pair_required", "Provide both minimum and maximum salary, or neither.");

        if (request.Headcount is < 1 or > 500)
            throw AppException.BadRequest("jobs.headcount_invalid", "Headcount must be between 1 and 500.");

        return emirate;
    }

    private async Task<IReadOnlyList<Guid>> ValidateSkillsAsync(UpsertVacancyRequest request, CancellationToken ct)
    {
        var distinct = request.SkillIds.Distinct().ToList();
        if (distinct.Count is < 1 or > Vacancy.MaxSkills)
            throw AppException.BadRequest(
                "jobs.skills_invalid", $"Provide between 1 and {Vacancy.MaxSkills} required skills.");

        foreach (var skillId in distinct)
        {
            if (!await skillCatalog.SkillExistsAsync(skillId, ct))
                throw AppException.BadRequest("jobs.skill_unknown", $"Unknown skill: {skillId}.");
        }

        return distinct;
    }

    private async Task<VacancyFeedItemResponse> ToFeedItemAsync(Vacancy vacancy, CancellationToken ct)
    {
        var company = await companies.FindByIdAsync(vacancy.CompanyId, ct);
        return new VacancyFeedItemResponse(
            vacancy.Id,
            vacancy.Title,
            company?.Name ?? string.Empty,
            company?.VerificationState.ToString() ?? string.Empty,
            vacancy.Emirate.ToString(),
            vacancy.Area,
            vacancy.SalaryMinAed,
            vacancy.SalaryMaxAed,
            vacancy.Headcount,
            await ToSkillItemsAsync(vacancy.Id, ct),
            vacancy.CreatedAt);
    }

    private async Task<IReadOnlyList<VacancySkillItem>> ToSkillItemsAsync(Guid vacancyId, CancellationToken ct)
    {
        var skills = await vacancies.ListSkillsAsync(vacancyId, ct);
        var items = new List<VacancySkillItem>();
        foreach (var vacancySkill in skills)
        {
            var skill = await skillCatalog.FindSkillAsync(vacancySkill.SkillId, ct);
            if (skill is null)
                continue; // reference data removed by an admin; keep the response honest
            items.Add(new VacancySkillItem(skill.Id, skill.NameEn, skill.NameAr, skill.RequiresCertification));
        }

        return items;
    }

    private async Task<CompanyVacancySummaryResponse> ToSummaryWithCountAsync(Vacancy vacancy, CancellationToken ct)
    {
        var counts = await applications.CountByVacancyAsync([vacancy.Id], ct);
        return ToSummary(vacancy, counts.TryGetValue(vacancy.Id, out var count) ? count : 0);
    }

    private static CompanyVacancySummaryResponse ToSummary(Vacancy vacancy, int applicationCount) => new(
        vacancy.Id,
        vacancy.Title,
        vacancy.Status.ToString(),
        vacancy.Emirate.ToString(),
        vacancy.Area,
        vacancy.SalaryMinAed,
        vacancy.SalaryMaxAed,
        vacancy.Headcount,
        applicationCount,
        vacancy.CreatedAt);
}
