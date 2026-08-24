namespace KaamLink.Application.Assessments;

// GET api/v1/workers/me/assessments — one row per skill on the caller's profile.
// VerificationState mirrors the SkillVerificationState enum NAME (trust contract).
public sealed record MyAssessmentItemResponse(
    Guid SkillId,
    string SkillNameEn,
    string SkillNameAr,
    bool RequiresCertification,
    bool AssessmentAvailable,
    string VerificationState,
    int? LatestScorePercent,
    bool? LatestPassed);

// POST .../attempts — the questions of the started attempt.
// SECURITY: this item deliberately has NO correct-answer field (§50).
public sealed record AssessmentQuestionItem(
    Guid Id,
    int SortOrder,
    string TextEn,
    string TextAr,
    string OptionAEn,
    string OptionAAr,
    string OptionBEn,
    string OptionBAr,
    string OptionCEn,
    string OptionCAr,
    string OptionDEn,
    string OptionDAr);

public sealed record StartAssessmentResponse(
    Guid AttemptId,
    Guid SkillId,
    int PassMarkPercent,
    int TotalQuestions,
    IReadOnlyList<AssessmentQuestionItem> Questions);

// POST .../attempts/{attemptId}/submit
public sealed record SubmitAssessmentAnswerItem(Guid QuestionId, int SelectedOptionIndex);

public sealed record SubmitAssessmentRequest(IReadOnlyList<SubmitAssessmentAnswerItem> Answers);

public sealed record AssessmentResultResponse(
    Guid AttemptId,
    int CorrectAnswers,
    int TotalQuestions,
    int ScorePercent,
    bool Passed,
    string VerificationState);
