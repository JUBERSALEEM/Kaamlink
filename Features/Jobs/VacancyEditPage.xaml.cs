namespace KaamLink.Features.Jobs
{
    public partial class VacancyEditPage : ContentPage
    {
        public VacancyEditPage(VacancyEditViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is VacancyEditViewModel viewModel)
                viewModel.LoadCommand.Execute(null);
        }
    }
}
