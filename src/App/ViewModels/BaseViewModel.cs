using Arisoul.Core.Maui.Models;

namespace Arisoul.T212.App.ViewModels;

public partial class BaseViewModel 
    : ArisoulMauiBaseViewModel
{
    [ObservableProperty]
    private bool _hasError;
}
