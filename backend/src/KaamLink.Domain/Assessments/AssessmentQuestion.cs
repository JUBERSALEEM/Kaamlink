namespace KaamLink.Domain.Assessments;

/// <summary>
/// One MCQ question: a bilingual statement and four bilingual options.
/// CorrectOptionIndex (0..3) is server-only data — never serialize it
/// (AssessmentQuestionItem in the read contract has no such property).
/// </summary>
public sealed class AssessmentQuestion
{
    public const int OptionCount = 4;

    public Guid Id { get; private set; }

    public Guid AssessmentId { get; private set; }

    public SkillAssessment Assessment { get; private set; } = default!;

    public string TextEn { get; private set; } = default!;

    public string TextAr { get; private set; } = default!;

    public string OptionAEn { get; private set; } = default!;

    public string OptionAAr { get; private set; } = default!;

    public string OptionBEn { get; private set; } = default!;

    public string OptionBAr { get; private set; } = default!;

    public string OptionCEn { get; private set; } = default!;

    public string OptionCAr { get; private set; } = default!;

    public string OptionDEn { get; private set; } = default!;

    public string OptionDAr { get; private set; } = default!;

    public int CorrectOptionIndex { get; private set; }

    public int SortOrder { get; private set; }

    private AssessmentQuestion()
    {
    }

    public static AssessmentQuestion Create(
        Guid id,
        Guid assessmentId,
        string textEn,
        string textAr,
        string optionAEn,
        string optionAAr,
        string optionBEn,
        string optionBAr,
        string optionCEn,
        string optionCAr,
        string optionDEn,
        string optionDAr,
        int correctOptionIndex,
        int sortOrder)
    {
        if (correctOptionIndex is < 0 or >= OptionCount)
            throw new ArgumentOutOfRangeException(nameof(correctOptionIndex));

        return new AssessmentQuestion
        {
            Id = id,
            AssessmentId = assessmentId,
            TextEn = textEn,
            TextAr = textAr,
            OptionAEn = optionAEn,
            OptionAAr = optionAAr,
            OptionBEn = optionBEn,
            OptionBAr = optionBAr,
            OptionCEn = optionCEn,
            OptionCAr = optionCAr,
            OptionDEn = optionDEn,
            OptionDAr = optionDAr,
            CorrectOptionIndex = correctOptionIndex,
            SortOrder = sortOrder
        };
    }

    public bool IsCorrect(int selectedOptionIndex) => selectedOptionIndex == CorrectOptionIndex;
}
