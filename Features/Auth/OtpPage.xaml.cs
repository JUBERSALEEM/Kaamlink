namespace KaamLink.Features.Auth
{
    public partial class OtpPage : ContentPage
    {
        private readonly OtpViewModel _viewModel;

        public OtpPage(OtpViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.OnAppearing();
        }

        protected override void OnDisappearing()
        {
            _viewModel.Dispose();
            base.OnDisappearing();
        }
    }
}
