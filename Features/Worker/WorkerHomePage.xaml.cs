namespace KaamLink.Features.Worker
{
    public partial class WorkerHomePage : ContentPage
    {
        public WorkerHomePage(WorkerHomeViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is WorkerHomeViewModel viewModel)
                viewModel.LoadCommand.Execute(null);
        }
    }
}
