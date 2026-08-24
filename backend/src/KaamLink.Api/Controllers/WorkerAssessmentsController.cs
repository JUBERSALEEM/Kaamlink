using KaamLink.Application.Abstractions;
using KaamLink.Application.Assessments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaamLink.Api.Controllers;

/// <summary>
/// Worker-facing skill assessments (Phase 05): list my skills with assessment
/// status, start an attempt (questions WITHOUT correct answers), submit answers
/// (server-side scoring). Every mutation is owned by the caller's worker profile.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/workers/me/assessments")]
public sealed class WorkerAssessmentsController(
    ISkillAssessmentService skillAssessments,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>One row per skill on the caller's profile: availability, verification state, last score.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MyAssessmentItemResponse>>> ListMine(
        CancellationToken cancellationToken) =>
        Ok(await skillAssessments.ListMineAsync(currentUser.UserId, cancellationToken));

    /// <summary>Starts a scored attempt and returns its questions (server never sends correct answers).</summary>
    [HttpPost("{skillId:guid}/attempts")]
    public async Task<ActionResult<StartAssessmentResponse>> Start(
        Guid skillId, CancellationToken cancellationToken) =>
        Ok(await skillAssessments.StartAsync(currentUser.UserId, skillId, cancellationToken));

    /// <summary>Scores the attempt; a pass marks the skill AssessmentVerified server-side.</summary>
    [HttpPost("{skillId:guid}/attempts/{attemptId:guid}/submit")]
    public async Task<ActionResult<AssessmentResultResponse>> Submit(
        Guid skillId,
        Guid attemptId,
        [FromBody] SubmitAssessmentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await skillAssessments.SubmitAsync(
            currentUser.UserId, skillId, attemptId, request, cancellationToken));
}
