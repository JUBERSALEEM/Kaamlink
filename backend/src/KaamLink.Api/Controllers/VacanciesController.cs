using KaamLink.Application.Abstractions;
using KaamLink.Application.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaamLink.Api.Controllers;

/// <summary>
/// Worker-facing jobs: the active-vacancy feed, vacancy detail, applying, and the
/// caller's own applications. Non-active vacancies are invisible here (404).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/vacancies")]
public sealed class VacanciesController(
    IVacancyService vacancies,
    IJobApplicationService jobApplications,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Feed: page from 1, pageSize ≤ 50; filters are ANDed.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<VacancyFeedItemResponse>>> Search(
        [FromQuery] string? emirate,
        [FromQuery] Guid? skillId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await vacancies.SearchActiveAsync(emirate, skillId, q, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VacancyDetailResponse>> GetDetail(
        Guid id, CancellationToken cancellationToken) =>
        Ok(await vacancies.GetActiveDetailAsync(currentUser.UserId, id, cancellationToken));

    [HttpPost("{id:guid}/applications")]
    public async Task<ActionResult<ApplicationItemResponse>> Apply(
        Guid id,
        [FromBody] ApplyRequest request,
        CancellationToken cancellationToken) =>
        Ok(await jobApplications.ApplyAsync(currentUser.UserId, id, request, cancellationToken));
}
