namespace KaamLink.Features.Employer
{
    public partial class EmployerOnboardingPage : ContentPage
    {
        public EmployerOnboardingPage(EmployerOnboardingViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
