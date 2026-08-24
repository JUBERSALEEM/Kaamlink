using KaamLink.Application.Abstractions;
using KaamLink.Domain.Assessments;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Assessments;

public sealed class AssessmentRepository(AppDbContext db) : IAssessmentRepository
{
    public Task<SkillAssessment?> FindActiveTemplateBySkillIdAsync(Guid skillId, CancellationToken ct = default) =>
        db.SkillAssessments.FirstOrDefaultAsync(a => a.SkillId == skillId && a.IsActive, ct);

    public async Task<IReadOnlyList<SkillAssessment>> ListActiveTemplatesAsync(CancellationToken ct = default) =>
        await db.SkillAssessments
            .Where(a => a.IsActive)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AssessmentQuestion>> ListQuestionsAsync(
        Guid assessmentId, CancellationToken ct = default) =>
        await db.AssessmentQuestions
            .Where(q => q.AssessmentId == assessmentId)
            .OrderBy(q => q.SortOrder)
            .ToListAsync(ct);

    public Task<AssessmentAttempt?> FindAttemptAsync(Guid attemptId, CancellationToken ct = default) =>
        db.AssessmentAttempts.FirstOrDefaultAsync(a => a.Id == attemptId, ct);

    public async Task<IReadOnlyList<AssessmentAttempt>> ListAttemptsByWorkerAsync(
        Guid workerProfileId, CancellationToken ct = default) =>
        await db.AssessmentAttempts
            .Where(a => a.WorkerProfileId == workerProfileId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

    public async Task AddAsync(AssessmentAttempt attempt, CancellationToken ct = default) =>
        await db.AssessmentAttempts.AddAsync(attempt, ct);
}
