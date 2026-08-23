namespace KaamLink.Features.Onboarding
{
    public partial class RoleSelectionPage : ContentPage
    {
        public RoleSelectionPage(RoleSelectionViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
