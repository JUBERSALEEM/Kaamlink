namespace KaamLink.Domain.Assessments;

/// <summary>
/// One selected option per question. Kept forever so a passed skill can always
/// be traced back to an actual scored attempt — trust is never asserted, it is
/// computed and auditable.
/// </summary>
public sealed class AssessmentAnswer
{
    public Guid Id { get; private set; }

    public Guid AttemptId { get; private set; }

    public AssessmentAttempt Attempt { get; private set; } = default!;

    public Guid QuestionId { get; private set; }

    public int SelectedOptionIndex { get; private set; }

    private AssessmentAnswer()
    {
    }

    public static AssessmentAnswer Create(
        Guid id, Guid attemptId, Guid questionId, int selectedOptionIndex)
    {
        if (selectedOptionIndex is < 0 or >= AssessmentQuestion.OptionCount)
            throw new ArgumentOutOfRangeException(nameof(selectedOptionIndex));

        return new AssessmentAnswer
        {
            Id = id,
            AttemptId = attemptId,
            QuestionId = questionId,
            SelectedOptionIndex = selectedOptionIndex
        };
    }
}
