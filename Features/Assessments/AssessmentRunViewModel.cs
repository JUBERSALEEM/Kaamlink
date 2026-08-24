using System.Collections.ObjectModel;
using KaamLink.Core.Api;
using KaamLink.Core.Navigation;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Assessments
{
    /// <summary>One question being answered (selection state lives here, not in XAML).</summary>
    public partial class QuestionRowModel : ObservableObject
    {
        public Guid Id { get; }

        public string Text { get; }

        public List<OptionRowModel> Options { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnswered))]
        private int? _selectedIndex;

        public bool IsAnswered => SelectedIndex.HasValue;

        public QuestionRowModel(Guid id, string text)
        {
            Id = id;
            Text = text;
        }
    }

    /// <summary>One selectable option of a question.</summary>
    public partial class OptionRowModel : ObservableObject
    {
        public QuestionRowModel Question { get; }

        public int Index { get; }

        public string Text { get; }

        [ObservableProperty]
        private bool _isSelected;

        public OptionRowModel(QuestionRowModel question, int index, string text)
        {
            Question = question;
            Index = index;
            Text = text;
        }
    }

    /// <summary>
    /// Assessment runner: starts a server-scored attempt, renders the questions the
    /// API returned (correct answers are never in the payload), collects one answer
    /// per question and submits once — the result panel shows the SERVER's verdict.
    /// IQueryAttributable by hand: ?skillId=/&amp;skillName= arrive as strings.
    /// </summary>
    public partial class AssessmentRunViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IApiClient _api;
        private Guid _skillId;
        private Guid _attemptId;

        public AssessmentRunViewModel(IApiClient api) => _api = api;

        [ObservableProperty]
        private string _skillName = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isLoaded;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string _passMarkText = string.Empty;

        [ObservableProperty]
        private string _answeredCountText = string.Empty;

        [ObservableProperty]
        private bool _hasResult;

        [ObservableProperty]
        private bool _resultPassed;

        [ObservableProperty]
        private string _resultTitle = string.Empty;

        /// <summary>State-token name for the StatusColorConverter (AssessmentVerified/Rejected).</summary>
        [ObservableProperty]
        private string _resultState = "SelfDeclared";

        [ObservableProperty]
        private string _resultScoreText = string.Empty;

        [ObservableProperty]
        private string _resultNote = string.Empty;

        public ObservableCollection<QuestionRowModel> Questions { get; } = [];

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("skillId", out var skillIdValue)
                && Guid.TryParse(skillIdValue?.ToString(), out var skillId))
            {
                _skillId = skillId;
            }

            if (query.TryGetValue("skillName", out var skillNameValue))
                SkillName = Uri.UnescapeDataString(skillNameValue?.ToString() ?? string.Empty);

            if (_attemptId == Guid.Empty)
                _ = StartAsync();
        }

        [RelayCommand]
        private async Task StartAsync()
        {
            if (IsBusy || _skillId == Guid.Empty)
                return;

            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            try
            {
                var started = await _api.StartAssessmentAsync(_skillId);
                _attemptId = started.AttemptId;
                PassMarkText = string.Format(AppStrings.AssessmentPassMarkFormat, started.PassMarkPercent);

                Questions.Clear();
                foreach (var question in started.Questions.OrderBy(q => q.SortOrder))
                {
                    var row = new QuestionRowModel(
                        question.Id, LocalizationHelpers.Localized(question.TextEn, question.TextAr));
                    var options = new (int Index, string En, string Ar)[]
                    {
                        (0, question.OptionAEn, question.OptionAAr),
                        (1, question.OptionBEn, question.OptionBAr),
                        (2, question.OptionCEn, question.OptionCAr),
                        (3, question.OptionDEn, question.OptionDAr)
                    };
                    foreach (var option in options)
                        row.Options.Add(new OptionRowModel(
                            row, option.Index, LocalizationHelpers.Localized(option.En, option.Ar)));
                    Questions.Add(row);
                }

                UpdateAnsweredCount();
                IsLoaded = true;
            }
            catch (ApiException exception) when (SessionErrors.IsExpiredSession(exception))
            {
                await SessionErrors.RouteToWelcomeAsync();
            }
            catch (ApiException exception)
            {
                HasError = true;
                ErrorMessage = exception.Code switch
                {
                    ApiClient.Codes.AssessmentNotAvailable => AppStrings.ErrorAssessmentNotAvailable,
                    ApiClient.Codes.AssessmentSkillNotOnProfile => AppStrings.ErrorAssessmentSkillNotOnProfile,
                    ApiClient.Codes.AssessmentAlreadyCompleted => AppStrings.ErrorAssessmentAlreadyCompleted,
                    _ => exception.IsOffline ? AppStrings.ErrorOffline : AppStrings.ErrorGeneric
                };
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectOption(OptionRowModel? option)
        {
            if (option is null || HasResult)
                return;

            foreach (var other in option.Question.Options)
                other.IsSelected = ReferenceEquals(other, option);
            option.Question.SelectedIndex = option.Index;

            UpdateAnsweredCount();
            SubmitCommand.NotifyCanExecuteChanged();
        }

        private bool CanSubmitAll() =>
            !IsBusy
            && !HasResult
            && Questions.Count > 0
            && Questions.All(q => q.IsAnswered);

        [RelayCommand(CanExecute = nameof(CanSubmitAll))]
        private async Task SubmitAsync()
        {
            IsBusy = true;
            try
            {
                var answers = Questions
                    .Select(q => new SubmitAssessmentAnswerItemDto(q.Id, q.SelectedIndex!.Value))
                    .ToList();
                var result = await _api.SubmitAssessmentAsync(_skillId, _attemptId, answers);

                HasResult = true;
                ResultPassed = result.Passed;
                ResultState = result.Passed ? "AssessmentVerified" : "Rejected";
                ResultTitle = result.Passed
                    ? AppStrings.AssessmentResultPassedTitle
                    : AppStrings.AssessmentResultFailedTitle;
                ResultScoreText = string.Format(
                    AppStrings.AssessmentResultScoreFormat,
                    result.CorrectAnswers,
                    result.TotalQuestions,
                    result.ScorePercent);
                ResultNote = result.Passed
                    ? AppStrings.AssessmentPassedNote
                    : AppStrings.AssessmentFailedNote;
            }
            catch (ApiException exception) when (SessionErrors.IsExpiredSession(exception))
            {
                await SessionErrors.RouteToWelcomeAsync();
            }
            catch (ApiException exception)
            {
                HasError = true;
                ErrorMessage = exception.Code switch
                {
                    ApiClient.Codes.AssessmentAlreadyCompleted => AppStrings.ErrorAssessmentAlreadyCompleted,
                    _ => exception.IsOffline ? AppStrings.ErrorOffline : AppStrings.ErrorGeneric
                };
            }
            finally
            {
                IsBusy = false;
                SubmitCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private Task DoneAsync() =>
            Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.MyAssessments));

        private void UpdateAnsweredCount() => AnsweredCountText = string.Format(
            AppStrings.AssessmentAnsweredFormat,
            Questions.Count(q => q.IsAnswered),
            Questions.Count);
    }
}
