namespace Arisoul.T212.App.Views;

public partial class SettingsPage : ContentPage
{
    SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
}