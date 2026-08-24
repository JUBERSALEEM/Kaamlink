namespace KaamLink.Features.Jobs
{
    public partial class VacancyApplicationsPage : ContentPage
    {
        public VacancyApplicationsPage(VacancyApplicationsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is VacancyApplicationsViewModel viewModel)
                viewModel.LoadCommand.Execute(null);
        }
    }
}
