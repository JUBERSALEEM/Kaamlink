namespace KaamLink.Features.Jobs
{
    public partial class VacancyFeedPage : ContentPage
    {
        public VacancyFeedPage(VacancyFeedViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is VacancyFeedViewModel viewModel)
                viewModel.SearchCommand.Execute(null);
        }
    }
}
