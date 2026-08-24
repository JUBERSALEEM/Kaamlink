namespace KaamLink.Domain.Assessments;

/// <summary>
/// One worker's sitting of a skill assessment. Score fields stay null until
/// submit: an unfinished attempt proves nothing. Submit is single-shot (retrying
/// means a NEW attempt — history is never rewritten, §50 honesty).
/// </summary>
public sealed class AssessmentAttempt
{
    public Guid Id { get; private set; }

    public Guid WorkerProfileId { get; private set; }

    public Guid SkillId { get; private set; }

    public int TotalQuestions { get; private set; }

    public int? CorrectAnswers { get; private set; }

    public int? ScorePercent { get; private set; }

    public bool? Passed { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    private readonly List<AssessmentAnswer> _answers = [];

    public IReadOnlyCollection<AssessmentAnswer> Answers => _answers;

    public bool IsCompleted => CompletedAt is not null;

    private AssessmentAttempt()
    {
    }

    public static AssessmentAttempt Start(
        Guid id, Guid workerProfileId, Guid skillId, int totalQuestions, DateTimeOffset now) => new()
        {
            Id = id,
            WorkerProfileId = workerProfileId,
            SkillId = skillId,
            TotalQuestions = totalQuestions,
            StartedAt = now
        };

    /// <summary>Closes the attempt with its score; throws when submitted twice.</summary>
    public void Complete(int correctAnswers, int passMarkPercent, DateTimeOffset now)
    {
        if (IsCompleted)
            throw new InvalidOperationException("Assessment attempt is already completed.");

        CorrectAnswers = correctAnswers;
        ScorePercent = TotalQuestions == 0 ? 0 : (int)Math.Round(100.0 * correctAnswers / TotalQuestions);
        Passed = ScorePercent >= passMarkPercent;
        CompletedAt = now;
    }

    public void AddAnswer(AssessmentAnswer answer) => _answers.Add(answer);
}
