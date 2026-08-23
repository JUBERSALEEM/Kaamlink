namespace KaamLink.Features.Employer
{
    public partial class EmployerHomePage : ContentPage
    {
        public EmployerHomePage(EmployerHomeViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
