namespace KaamLink.Features.Assessments
{
    public partial class AssessmentRunPage : ContentPage
    {
        public AssessmentRunPage(AssessmentRunViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
