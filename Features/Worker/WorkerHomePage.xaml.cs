namespace KaamLink.Features.Worker
{
    public partial class WorkerHomePage : ContentPage
    {
        public WorkerHomePage(WorkerHomeViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
