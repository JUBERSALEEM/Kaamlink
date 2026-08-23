namespace KaamLink.Features.Employer
{
    public partial class EmployerHomePage : ContentPage
    {
        public EmployerHomePage(EmployerHomeViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is EmployerHomeViewModel viewModel)
                viewModel.LoadCommand.Execute(null);
        }
    }
}
