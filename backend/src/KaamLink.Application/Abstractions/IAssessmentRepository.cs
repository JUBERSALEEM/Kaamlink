using KaamLink.Domain.Assessments;

namespace KaamLink.Application.Abstractions;

public interface IAssessmentRepository
{
    /// <summary>The single active template for a skill, or null (assessment not offered yet).</summary>
    Task<SkillAssessment?> FindActiveTemplateBySkillIdAsync(Guid skillId, CancellationToken ct = default);

    /// <summary>All active templates (idempotent seed coverage) — used for the /me listing.</summary>
    Task<IReadOnlyList<SkillAssessment>> ListActiveTemplatesAsync(CancellationToken ct = default);

    /// <summary>Template questions in display order; scoring runs against this same set at submit.</summary>
    Task<IReadOnlyList<AssessmentQuestion>> ListQuestionsAsync(Guid assessmentId, CancellationToken ct = default);

    Task<AssessmentAttempt?> FindAttemptAsync(Guid attemptId, CancellationToken ct = default);

    Task<IReadOnlyList<AssessmentAttempt>> ListAttemptsByWorkerAsync(
        Guid workerProfileId, CancellationToken ct = default);

    Task AddAsync(AssessmentAttempt attempt, CancellationToken ct = default);
}
