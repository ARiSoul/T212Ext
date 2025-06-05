namespace Arisoul.T212.App.Views;

public partial class DividendsPage : ContentPage
{
	DividendsViewModel _viewModel;

    public DividendsPage(DividendsViewModel viewModel)
    {
		InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
     
        _viewModel.LoadDividendsCommand.Execute(null);
    }
}