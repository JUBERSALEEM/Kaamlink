namespace KaamLink.Features.Onboarding
{
    public partial class RoleSelectionPage : ContentPage
    {
        private readonly RoleSelectionViewModel _viewModel;

        public RoleSelectionPage(RoleSelectionViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = _viewModel.EnsureSessionRestoredAsync();
        }
    }
}
