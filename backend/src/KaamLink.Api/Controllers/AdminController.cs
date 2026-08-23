using KaamLink.Api.Configuration;
using KaamLink.Application.Abstractions;
using KaamLink.Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaamLink.Api.Controllers;

/// <summary>
/// Platform-admin operations (Admin Portal arrives later; V1 = secure admin-lite API).
/// Every endpoint requires the PlatformAdmin policy — membership/UI state is irrelevant here.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
[Route("api/v1/admin")]
public sealed class AdminController(
    ICompanyService companies,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Verification review queue (oldest pending first).</summary>
    [HttpGet("companies/pending")]
    public async Task<ActionResult<IReadOnlyList<CompanySummaryResponse>>> ListPendingCompanies(
        CancellationToken cancellationToken) =>
        Ok(await companies.ListPendingAsync(cancellationToken));

    /// <summary>Approve (Verified) or reject (Rejected + reason) a pending company.</summary>
    [HttpPost("companies/{companyId:guid}/review")]
    public async Task<ActionResult<CompanySummaryResponse>> ReviewCompany(
        Guid companyId,
        [FromBody] ReviewCompanyRequest request,
        CancellationToken cancellationToken) =>
        Ok(await companies.ReviewAsync(currentUser.UserId, companyId, request, cancellationToken));
}
