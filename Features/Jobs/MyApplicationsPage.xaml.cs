namespace KaamLink.Features.Jobs
{
    public partial class MyApplicationsPage : ContentPage
    {
        public MyApplicationsPage(MyApplicationsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is MyApplicationsViewModel viewModel)
                viewModel.LoadCommand.Execute(null);
        }
    }
}
