using KaamLink.Application.Abstractions;
using KaamLink.Application.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaamLink.Api.Controllers;

/// <summary>The caller's own job applications (worker side).</summary>
[ApiController]
[Authorize]
[Route("api/v1/workers/me/applications")]
public sealed class WorkerApplicationsController(
    IJobApplicationService jobApplications,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationItemResponse>>> GetMine(
        CancellationToken cancellationToken) =>
        Ok(await jobApplications.ListMineAsync(currentUser.UserId, cancellationToken));

    /// <summary>Withdraw while the employer's decision is still open (terminally states reject with 400).</summary>
    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<ApplicationItemResponse>> Withdraw(
        Guid id, CancellationToken cancellationToken) =>
        Ok(await jobApplications.WithdrawAsync(currentUser.UserId, id, cancellationToken));
}
