using Arisoul.Core.Maui.Models;

namespace Arisoul.T212.App.ViewModels;

public partial class BaseViewModel 
    : ArisoulMauiBaseViewModel
{
    [ObservableProperty]
    private bool _hasError;

    public bool IsWindows => DeviceInfo.Platform == DevicePlatform.WinUI;

    [RelayCommand]
    public async Task CopyTextToClipboardAsync(string text)
    {
        await Clipboard.Default.SetTextAsync(text).ConfigureAwait(false);
    }
}
