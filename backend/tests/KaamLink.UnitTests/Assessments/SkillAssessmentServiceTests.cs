using KaamLink.Application.Assessments;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;
using KaamLink.Domain.Workers;

namespace KaamLink.UnitTests.Assessments;

public sealed class SkillAssessmentServiceTests
{
    private static readonly Guid Welder = AssessmentTestHarness.WelderSkillId;
    private static readonly Guid Aluminium = AssessmentTestHarness.AluminiumSkillId;

    private static SubmitAssessmentRequest AnswersFrom(
        IReadOnlyList<AssessmentQuestionItem> questions, params int[] selections) => new(
        questions
            .OrderBy(q => q.SortOrder)
            .Take(selections.Length)
            .Select((q, i) => new SubmitAssessmentAnswerItem(q.Id, selections[i]))
            .ToList());

    [Fact]
    public async Task ListMine_ReturnsSkills_Availability_AndSelfDeclaredState()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, profile) = await harness.SeedWorkerWithSkillAsync(Welder);
        // The worker claims both welder (has a template) and aluminium (honest "no assessment yet").
        await harness.WorkerProfiles.ReplaceSkillsAsync(profile.Id,
        [
            WorkerSkill.Create(profile.Id, Welder, 5, true),
            WorkerSkill.Create(profile.Id, Aluminium, 2, false)
        ]);

        var items = await harness.CreateService().ListMineAsync(user.Id);

        Assert.Equal(2, items.Count);
        var welder = items.Single(i => i.SkillId == Welder);
        var aluminium = items.Single(i => i.SkillId == Aluminium);
        Assert.True(welder.AssessmentAvailable); // welder has a seeded template
        Assert.False(aluminium.AssessmentAvailable); // honest "no assessment yet" (§50)
        Assert.All(items, i => Assert.Equal("SelfDeclared", i.VerificationState));
    }

    [Fact]
    public async Task Start_RequiresProfile()
    {
        var harness = AssessmentTestHarness.Create();

        var exception = await Assert.ThrowsAsync<AppException>(
            () => harness.CreateService().StartAsync(Guid.NewGuid(), Welder));

        Assert.Equal("worker.profile_required", exception.Code);
    }

    [Fact]
    public async Task Start_SkillNotOnProfile_Throws()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, _) = await harness.SeedWorkerWithSkillAsync(Aluminium);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => harness.CreateService().StartAsync(user.Id, Welder));

        Assert.Equal("assessment.skill_not_on_profile", exception.Code);
    }

    [Fact]
    public async Task Start_TemplateMissing_ThrowsNotAvailable()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, _) = await harness.SeedWorkerWithSkillAsync(Aluminium);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => harness.CreateService().StartAsync(user.Id, Aluminium));

        Assert.Equal("assessment.not_available", exception.Code);
        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task Start_ReturnsQuestionsAndAudits_WithoutScoringAnything()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, profile) = await harness.SeedWorkerWithSkillAsync(Welder);

        var started = await harness.CreateService().StartAsync(user.Id, Welder);

        Assert.Equal(3, started.TotalQuestions);
        Assert.Equal(3, started.Questions.Count);
        Assert.Equal(60, started.PassMarkPercent);
        var attempt = Assert.Single(harness.Assessments.Attempts);
        Assert.Equal(profile.Id, attempt.WorkerProfileId);
        Assert.False(attempt.IsCompleted);
        Assert.Contains(harness.Audit.Records, r => r.Action == AuditActions.AssessmentAttemptStarted);
    }

    [Fact]
    public async Task Submit_AllCorrect_Passes_AndMarksSkillAssessmentVerified()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, profile) = await harness.SeedWorkerWithSkillAsync(Welder);
        var started = await harness.CreateService().StartAsync(user.Id, Welder);

        var result = await harness.CreateService().SubmitAsync(
            user.Id, Welder, started.AttemptId,
            AnswersFrom(started.Questions, AssessmentTestHarness.Context.CorrectOptions));

        Assert.True(result.Passed);
        Assert.Equal(100, result.ScorePercent);
        Assert.Equal(3, result.CorrectAnswers);
        Assert.Equal("AssessmentVerified", result.VerificationState);

        var skills = await harness.WorkerProfiles.ListSkillsAsync(profile.Id);
        var verified = Assert.Single(skills.Where(s => s.SkillId == Welder));
        Assert.Equal(SkillVerificationState.AssessmentVerified, verified.VerificationState);
        Assert.NotNull(verified.AssessmentVerifiedAt);
        Assert.Contains(harness.Audit.Records, r => r.Action == AuditActions.WorkerSkillAssessmentVerified);
        Assert.Contains(harness.Audit.Records, r => r.Action == AuditActions.AssessmentSubmitted);
    }

    [Fact]
    public async Task Submit_ExactlyAtPassMark_Passes()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, _) = await harness.SeedWorkerWithSkillAsync(Welder);
        var started = await harness.CreateService().StartAsync(user.Id, Welder);

        // 2/3 correct (66% >= 60%): correct options per question are 0, 1, 2.
        var result = await harness.CreateService().SubmitAsync(
            user.Id, Welder, started.AttemptId,
            AnswersFrom(started.Questions, 0, 1, 3 /* wrong */));

        Assert.True(result.Passed);
        Assert.Equal(67, result.ScorePercent);
    }

    [Fact]
    public async Task Submit_BelowPassMark_Fails_AndStateStaysSelfDeclared()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, _) = await harness.SeedWorkerWithSkillAsync(Welder);
        var started = await harness.CreateService().StartAsync(user.Id, Welder);

        var result = await harness.CreateService().SubmitAsync(
            user.Id, Welder, started.AttemptId,
            AnswersFrom(started.Questions, 3, 3, 3 /* only q3 correct: 33% */));

        Assert.False(result.Passed);
        Assert.Equal(33, result.ScorePercent);
        Assert.Equal("SelfDeclared", result.VerificationState);
        Assert.DoesNotContain(
            harness.Audit.Records, r => r.Action == AuditActions.WorkerSkillAssessmentVerified);
    }

    [Fact]
    public async Task Submit_Twice_ThrowsAlreadyCompleted()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, _) = await harness.SeedWorkerWithSkillAsync(Welder);
        var service = harness.CreateService();
        var started = await service.StartAsync(user.Id, Welder);
        await service.SubmitAsync(user.Id, Welder, started.AttemptId,
            AnswersFrom(started.Questions, 0, 1, 2));

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.SubmitAsync(user.Id, Welder, started.AttemptId,
                AnswersFrom(started.Questions, 0, 1, 2)));

        Assert.Equal("assessment.already_completed", exception.Code);
    }

    [Fact]
    public async Task Submit_ForeignAttempt_ThrowsForbidden()
    {
        var harness = AssessmentTestHarness.Create();
        var service = harness.CreateService();
        var (owner, _) = await harness.SeedWorkerWithSkillAsync(Welder);
        var started = await service.StartAsync(owner.Id, Welder);
        var (attacker, _) = await harness.SeedWorkerWithSkillAsync(Welder, phone: "+971509999999");

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.SubmitAsync(attacker.Id, Welder, started.AttemptId,
                AnswersFrom(started.Questions, 0, 1, 2)));

        Assert.Equal("assessment.attempt_forbidden", exception.Code);
        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public async Task Submit_InvalidAnswers_AreRejected()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, _) = await harness.SeedWorkerWithSkillAsync(Welder);
        var service = harness.CreateService();
        var started = await service.StartAsync(user.Id, Welder);

        // Unknown question id
        var unknown = await Assert.ThrowsAsync<AppException>(
            () => service.SubmitAsync(user.Id, Welder, started.AttemptId,
                new SubmitAssessmentRequest(
                    [new SubmitAssessmentAnswerItem(Guid.NewGuid(), 0)])));
        Assert.Equal("assessment.question_invalid", unknown.Code);

        // Out-of-range option
        var range = await Assert.ThrowsAsync<AppException>(
            () => service.SubmitAsync(user.Id, Welder, started.AttemptId,
                AnswersFrom(started.Questions, 9, 1, 2)));
        Assert.Equal("assessment.answer_invalid", range.Code);
    }

    [Fact]
    public async Task Retake_IsANewAttempt_HistoryIsKept()
    {
        var harness = AssessmentTestHarness.Create();
        var (user, _) = await harness.SeedWorkerWithSkillAsync(Welder);
        var service = harness.CreateService();
        var first = await service.StartAsync(user.Id, Welder);
        await service.SubmitAsync(user.Id, Welder, first.AttemptId,
            AnswersFrom(first.Questions, 3, 3, 3));

        var second = await service.StartAsync(user.Id, Welder);

        Assert.NotEqual(first.AttemptId, second.AttemptId);
        Assert.Equal(2, harness.Assessments.Attempts.Count);
    }
}
