namespace Arisoul.T212.App.Views;

public partial class OpenPositionsPage : ContentPage
{
	OpenPositionsViewModel _viewModel;

	public OpenPositionsPage(OpenPositionsViewModel viewModel)
	{
		InitializeComponent();

		BindingContext = _viewModel = viewModel;
	}
}
