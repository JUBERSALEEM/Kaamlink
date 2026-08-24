namespace KaamLink.Application.Jobs;

public interface IVacancyService
{
    /// <summary>Worker feed: active vacancies only, with filters (page from 1, pageSize ≤ 50).</summary>
    Task<PagedResponse<VacancyFeedItemResponse>> SearchActiveAsync(
        string? emirate, Guid? skillId, string? keyword, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Worker-facing detail; non-active vacancies are invisible (404) to workers.</summary>
    Task<VacancyDetailResponse> GetActiveDetailAsync(Guid userId, Guid vacancyId, CancellationToken ct = default);

    /// <summary>Employer: create a Draft vacancy (Owner/Admin/HiringManager only).</summary>
    Task<CompanyVacancySummaryResponse> CreateAsync(
        Guid userId, Guid companyId, UpsertVacancyRequest request, CancellationToken ct = default);

    Task<CompanyVacancySummaryResponse> UpdateAsync(
        Guid userId, Guid companyId, Guid vacancyId, UpsertVacancyRequest request, CancellationToken ct = default);

    /// <summary>Lifecycle transition; activation additionally requires a Verified company.</summary>
    Task<CompanyVacancySummaryResponse> ChangeStatusAsync(
        Guid userId, Guid companyId, Guid vacancyId, ChangeVacancyStatusRequest request, CancellationToken ct = default);

    /// <summary>Employer list (any member can read; any status).</summary>
    Task<IReadOnlyList<CompanyVacancySummaryResponse>> ListCompanyVacanciesAsync(
        Guid userId, Guid companyId, CancellationToken ct = default);
}
