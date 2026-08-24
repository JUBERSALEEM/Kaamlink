namespace KaamLink.Features.Jobs
{
    public partial class CompanyVacanciesPage : ContentPage
    {
        public CompanyVacanciesPage(CompanyVacanciesViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is CompanyVacanciesViewModel viewModel)
                viewModel.LoadCommand.Execute(null);
        }
    }
}
