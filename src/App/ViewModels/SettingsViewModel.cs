using Arisoul.T212.Client;

namespace Arisoul.T212.App.ViewModels;

public partial class SettingsViewModel 
    : BaseViewModel
{
    [ObservableProperty]
    private IT212ClientOptions _clientOptions;

    public SettingsViewModel(IServiceProvider serviceProvider)
    {
        _clientOptions = serviceProvider.GetRequiredService<IT212ClientOptions>();
        _clientOptions.LoadOptions();
    }

    [RelayCommand]
    public void Save()
    {
        ClientOptions.IsInitiated = true;
        ClientOptions.SaveOptions(markAsChanged: true);
    }
}
