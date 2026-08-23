namespace KaamLink.Features.Worker
{
    public partial class WorkerOnboardingPage : ContentPage
    {
        public WorkerOnboardingPage(WorkerOnboardingViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is WorkerOnboardingViewModel viewModel)
                viewModel.LoadCommand.Execute(null);
        }
    }
}
