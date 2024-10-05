namespace Arisoul.T212.App.Views;

public partial class MainPage : ContentPage
{
	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

    private async void ShowCashButton_Clicked(object sender, EventArgs e)
    {
        // Rotate button to the middle (90 degrees)
        await ShowCashButton.RotateYTo(180, 250, Easing.Linear);

        // Rotate from the middle to the end (180 degrees) to simulate a flip
        await HideCashButton.RotateYTo(0, 50, Easing.Linear);
    }

    private async void HideCashButton_Clicked(object sender, EventArgs e)
    {
        // Rotate button to the middle (90 degrees)
        await HideCashButton.RotateYTo(180, 250, Easing.Linear);

        // Rotate from the middle to the end (180 degrees) to simulate a flip
        await ShowCashButton.RotateYTo(0, 50, Easing.Linear);
    }
}
