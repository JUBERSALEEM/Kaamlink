namespace KaamLink.Features.Auth
{
    public partial class LoginPage : ContentPage
    {
        private readonly LoginViewModel _viewModel;

        public LoginPage(LoginViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = SafeInitializeAsync();
        }

        // OnAppearing cannot be async. An unobserved exception here used to surface
        // as a random "KaamLink keeps stopping" after the login screen appeared.
        private async Task SafeInitializeAsync()
        {
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] login initialization failed: {exception}");
            }
        }
    }
}
