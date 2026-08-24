namespace KaamLink.Application.Assessments;

public interface ISkillAssessmentService
{
    Task<IReadOnlyList<MyAssessmentItemResponse>> ListMineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Starts a scored attempt: template + questions (no correct answers) + attempt id.</summary>
    Task<StartAssessmentResponse> StartAsync(Guid userId, Guid skillId, CancellationToken ct = default);

    Task<AssessmentResultResponse> SubmitAsync(
        Guid userId, Guid skillId, Guid attemptId, SubmitAssessmentRequest request, CancellationToken ct = default);
}
