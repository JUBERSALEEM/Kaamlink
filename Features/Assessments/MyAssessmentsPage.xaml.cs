namespace KaamLink.Features.Assessments
{
    public partial class MyAssessmentsPage : ContentPage
    {
        public MyAssessmentsPage(MyAssessmentsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is MyAssessmentsViewModel viewModel)
                viewModel.LoadCommand.Execute(null);
        }
    }
}
