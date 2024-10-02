namespace Arisoul.T212.App.Views;

public partial class OpenPositionsDetailPage : ContentPage
{
	public OpenPositionsDetailPage(OpenPositionsDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
