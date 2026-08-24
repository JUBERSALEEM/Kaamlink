using KaamLink.Application.Abstractions;
using KaamLink.Application.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaamLink.Api.Controllers;

/// <summary>
/// Employer-facing vacancy management, strictly company-scoped: membership and
/// role are re-verified in the application layer for every call (never trusted
/// from the client). Reads allow any member; writes require Owner/Admin/HiringManager.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/companies/{companyId:guid}/vacancies")]
public sealed class CompanyVacanciesController(
    IVacancyService vacancies,
    IJobApplicationService jobApplications,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanyVacancySummaryResponse>>> List(
        Guid companyId, CancellationToken cancellationToken) =>
        Ok(await vacancies.ListCompanyVacanciesAsync(currentUser.UserId, companyId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CompanyVacancySummaryResponse>> Create(
        Guid companyId,
        [FromBody] UpsertVacancyRequest request,
        CancellationToken cancellationToken) =>
        Ok(await vacancies.CreateAsync(currentUser.UserId, companyId, request, cancellationToken));

    [HttpPut("{vacancyId:guid}")]
    public async Task<ActionResult<CompanyVacancySummaryResponse>> Update(
        Guid companyId,
        Guid vacancyId,
        [FromBody] UpsertVacancyRequest request,
        CancellationToken cancellationToken) =>
        Ok(await vacancies.UpdateAsync(currentUser.UserId, companyId, vacancyId, request, cancellationToken));

    /// <summary>Lifecycle: Draft→Active (Verified company required), Active↔Paused, →Closed.</summary>
    [HttpPost("{vacancyId:guid}/status")]
    public async Task<ActionResult<CompanyVacancySummaryResponse>> ChangeStatus(
        Guid companyId,
        Guid vacancyId,
        [FromBody] ChangeVacancyStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await vacancies.ChangeStatusAsync(currentUser.UserId, companyId, vacancyId, request, cancellationToken));

    /// <summary>Applicants of one vacancy (contains phone numbers — hiring roles only).</summary>
    [HttpGet("{vacancyId:guid}/applications")]
    public async Task<ActionResult<IReadOnlyList<VacancyApplicationItemResponse>>> ListApplications(
        Guid companyId, Guid vacancyId, CancellationToken cancellationToken) =>
        Ok(await jobApplications.ListForVacancyAsync(currentUser.UserId, companyId, vacancyId, cancellationToken));

    /// <summary>Employer decision: Viewed / Shortlisted / Rejected / Hired (terminal states are final).</summary>
    [HttpPost("{vacancyId:guid}/applications/{applicationId:guid}/status")]
    public async Task<ActionResult<ApplicationStatusChangeResponse>> ChangeApplicationStatus(
        Guid companyId,
        Guid vacancyId,
        Guid applicationId,
        [FromBody] ChangeApplicationStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await jobApplications.ChangeStatusAsync(
            currentUser.UserId, companyId, vacancyId, applicationId, request, cancellationToken));
}
