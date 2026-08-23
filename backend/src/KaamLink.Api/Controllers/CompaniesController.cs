using KaamLink.Application.Abstractions;
using KaamLink.Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaamLink.Api.Controllers;

/// <summary>Company onboarding + the caller's own memberships (membership-scoped).</summary>
[ApiController]
[Authorize]
[Route("api/v1/companies")]
public sealed class CompaniesController(
    ICompanyService companies,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CompanySummaryResponse>> Create(
        [FromBody] CreateCompanyRequest request,
        CancellationToken cancellationToken) =>
        Ok(await companies.CreateAsync(currentUser.UserId, request, cancellationToken));

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<CompanySummaryResponse>>> GetMine(
        CancellationToken cancellationToken) =>
        Ok(await companies.GetMineAsync(currentUser.UserId, cancellationToken));
}
