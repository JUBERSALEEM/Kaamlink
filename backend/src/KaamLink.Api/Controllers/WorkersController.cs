using KaamLink.Application.Abstractions;
using KaamLink.Application.Workers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaamLink.Api.Controllers;

/// <summary>
/// The caller's own worker profile (progressive onboarding + Skill Passport).
/// All endpoints are self-scoped by the JWT subject — a worker can only ever
/// read/write their own profile.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/workers")]
public sealed class WorkersController(
    IWorkerProfileService workerProfiles,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<WorkerProfileResponse>> GetMyProfile(CancellationToken cancellationToken) =>
        Ok(await workerProfiles.GetMineAsync(currentUser.UserId, cancellationToken));

    /// <summary>Progressive patch; creates the profile on first call. Only provided fields change.</summary>
    [HttpPut("me")]
    public async Task<ActionResult<WorkerProfileResponse>> UpdateMyProfile(
        [FromBody] UpdateWorkerProfileRequest request,
        CancellationToken cancellationToken) =>
        Ok(await workerProfiles.UpdateProfileAsync(currentUser.UserId, request, cancellationToken));

    /// <summary>Full replacement of the caller's self-declared skills.</summary>
    [HttpPut("me/skills")]
    public async Task<ActionResult<WorkerProfileResponse>> SetMySkills(
        [FromBody] SetWorkerSkillsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await workerProfiles.SetSkillsAsync(currentUser.UserId, request, cancellationToken));
}
