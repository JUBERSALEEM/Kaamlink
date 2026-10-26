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
            _ = SafeInitializeAsync();
        }

        private async Task SafeInitializeAsync()
        {
            try
            {
                await _viewModel.EnsureSessionRestoredAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] role selection session restore failed: {exception}");
            }
        }
    }
}
