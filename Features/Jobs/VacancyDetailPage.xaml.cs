namespace KaamLink.Features.Jobs
{
    public partial class VacancyDetailPage : ContentPage
    {
        public VacancyDetailPage(VacancyDetailViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is VacancyDetailViewModel viewModel)
                viewModel.LoadCommand.Execute(null);
        }
    }
}
