using System.Text.Json;
using KaamLink.Application.Abstractions;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;
using KaamLink.Domain.Assessments;

namespace KaamLink.Application.Assessments;

/// <summary>
/// Skill assessments — the trust spine of the Skill Passport (Master Prompt: workers
/// prove skills, employers buy trust). Rules (documented in the Phase 05 report):
/// assessment requires completed worker onboarding; the skill must be ON the worker's
/// profile (assessment exists to verify a claim); scoring is 100% server-side; correct
/// answers never leave the API; submit is single-shot, retakes create new attempts;
/// a pass writes AssessmentVerified + timestamp to the WorkerSkill — the ONLY writer.
/// </summary>
public sealed class SkillAssessmentService(
    IAssessmentRepository assessments,
    IWorkerProfileRepository workerProfiles,
    ISkillCatalogRepository skills,
    IUnitOfWork unitOfWork,
    IAuditService audit,
    TimeProvider timeProvider) : ISkillAssessmentService
{
    public async Task<IReadOnlyList<MyAssessmentItemResponse>> ListMineAsync(
        Guid userId, CancellationToken ct = default)
    {
        var (profile, workerSkills) = await RequireWorkerAsync(userId, ct);
        var templates = await assessments.ListActiveTemplatesAsync(ct);
        var availableSkillIds = templates.Select(t => t.SkillId).ToHashSet();
        var attempts = await assessments.ListAttemptsByWorkerAsync(profile.Id, ct);

        var items = new List<MyAssessmentItemResponse>(workerSkills.Count);
        foreach (var workerSkill in workerSkills)
        {
            var skill = await skills.FindSkillAsync(workerSkill.SkillId, ct);
            var latest = attempts
                .Where(a => a.SkillId == workerSkill.SkillId && a.IsCompleted)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefault();

            items.Add(new MyAssessmentItemResponse(
                workerSkill.SkillId,
                skill?.NameEn ?? string.Empty,
                skill?.NameAr ?? string.Empty,
                skill?.RequiresCertification ?? false,
                availableSkillIds.Contains(workerSkill.SkillId),
                workerSkill.VerificationState.ToString(),
                latest?.ScorePercent,
                latest?.Passed));
        }

        return items;
    }

    public async Task<StartAssessmentResponse> StartAsync(
        Guid userId, Guid skillId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var (profile, workerSkills) = await RequireWorkerAsync(userId, ct);

        if (workerSkills.All(s => s.SkillId != skillId))
            throw AppException.BadRequest(
                "assessment.skill_not_on_profile", "Add this skill to your profile before taking its assessment.");

        var template = await assessments.FindActiveTemplateBySkillIdAsync(skillId, ct)
            ?? throw AppException.NotFound(
                "assessment.not_available", "No assessment is available for this skill yet.");

        var questions = await assessments.ListQuestionsAsync(template.Id, ct);
        if (questions.Count == 0)
            throw AppException.BadRequest(
                "assessment.no_questions", "This assessment has no questions yet. Please try again later.");

        var attempt = AssessmentAttempt.Start(
            Guid.NewGuid(), profile.Id, skillId, questions.Count, now);
        await assessments.AddAsync(attempt, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.AssessmentAttemptStarted,
            nameof(AssessmentAttempt),
            attempt.Id.ToString(),
            actorUserId: userId,
            metadataJson: JsonSerializer.Serialize(new { skillId }),
            cancellationToken: ct);

        return new StartAssessmentResponse(
            attempt.Id,
            skillId,
            template.PassMarkPercent,
            questions.Count,
            questions
                .Select(q => new AssessmentQuestionItem(
                    q.Id, q.SortOrder, q.TextEn, q.TextAr,
                    q.OptionAEn, q.OptionAAr, q.OptionBEn, q.OptionBAr,
                    q.OptionCEn, q.OptionCAr, q.OptionDEn, q.OptionDAr))
                .ToList());
    }

    public async Task<AssessmentResultResponse> SubmitAsync(
        Guid userId, Guid skillId, Guid attemptId, SubmitAssessmentRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var (profile, workerSkills) = await RequireWorkerAsync(userId, ct);

        var attempt = await assessments.FindAttemptAsync(attemptId, ct);
        if (attempt is null || attempt.SkillId != skillId)
            throw AppException.NotFound("assessment.attempt_not_found", "Assessment attempt not found.");
        if (attempt.WorkerProfileId != profile.Id)
            throw AppException.Forbidden("assessment.attempt_forbidden", "This attempt belongs to another worker.");
        if (attempt.IsCompleted)
            throw AppException.BadRequest("assessment.already_completed", "This attempt was already submitted. Start a new one to retry.");

        var template = await assessments.FindActiveTemplateBySkillIdAsync(skillId, ct)
            ?? throw AppException.NotFound(
                "assessment.not_available", "No assessment is available for this skill yet.");
        var questions = await assessments.ListQuestionsAsync(template.Id, ct);
        var questionById = questions.ToDictionary(q => q.Id);

        if (request.Answers.Count == 0)
            throw AppException.BadRequest("assessment.answers_required", "Answer the questions before submitting.");
        if (request.Answers.Select(a => a.QuestionId).Distinct().Count() != request.Answers.Count)
            throw AppException.BadRequest("assessment.answer_duplicate", "Each question can be answered only once.");
        foreach (var answer in request.Answers)
        {
            if (!questionById.ContainsKey(answer.QuestionId))
                throw AppException.BadRequest("assessment.question_invalid", "An answer references a question outside this assessment.");
            if (answer.SelectedOptionIndex is < 0 or >= AssessmentQuestion.OptionCount)
                throw AppException.BadRequest("assessment.answer_invalid", "Selected option is out of range.");
        }

        var correct = request.Answers.Count(a => questionById[a.QuestionId].IsCorrect(a.SelectedOptionIndex));
        attempt.Complete(correct, template.PassMarkPercent, now);
        foreach (var answer in request.Answers)
            attempt.AddAnswer(AssessmentAnswer.Create(
                Guid.NewGuid(), attempt.Id, answer.QuestionId, answer.SelectedOptionIndex));

        var verificationState = workerSkills.Single(s => s.SkillId == skillId);
        if (attempt.Passed == true)
        {
            verificationState.MarkAssessmentVerified(now);
            await audit.RecordAsync(
                AuditActions.WorkerSkillAssessmentVerified,
                nameof(Domain.Workers.WorkerSkill),
                skillId.ToString(),
                actorUserId: userId,
                metadataJson: JsonSerializer.Serialize(new { attemptId, attempt.ScorePercent }),
                cancellationToken: ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.AssessmentSubmitted,
            nameof(AssessmentAttempt),
            attempt.Id.ToString(),
            actorUserId: userId,
            metadataJson: JsonSerializer.Serialize(new { skillId, attempt.ScorePercent, attempt.Passed }),
            cancellationToken: ct);

        return new AssessmentResultResponse(
            attempt.Id,
            correct,
            attempt.TotalQuestions,
            attempt.ScorePercent!.Value,
            attempt.Passed!.Value,
            verificationState.VerificationState.ToString());
    }

    private async Task<(Domain.Workers.WorkerProfile Profile, IReadOnlyList<Domain.Workers.WorkerSkill> Skills)>
        RequireWorkerAsync(Guid userId, CancellationToken ct)
    {
        var profile = await workerProfiles.FindByUserIdAsync(userId, ct)
            ?? throw AppException.BadRequest("worker.profile_required", "Create your worker profile first.");
        if (!profile.OnboardingComplete)
            throw AppException.BadRequest("worker.onboarding_incomplete", "Complete your onboarding before taking assessments.");
        var workerSkills = await workerProfiles.ListSkillsAsync(profile.Id, ct);
        return (profile, workerSkills);
    }
}
