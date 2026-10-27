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

        /// <summary>
        /// App calls this after routing a legacy link deep link to the sign-in
        /// page. InitializeAsync also checks for a pending callback, and
        /// TryTakePending is one-shot, so at most one exchange can run.
        /// </summary>
        public Task HandleEmailAuthCallbackAsync() => _viewModel.ProcessPendingEmailAuthCallbackAsync();

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
