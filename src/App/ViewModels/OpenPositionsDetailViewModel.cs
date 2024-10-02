namespace Arisoul.T212.App.ViewModels;

[QueryProperty(nameof(Item), "Item")]
public partial class OpenPositionsDetailViewModel : BaseViewModel
{
	[ObservableProperty]
	SampleItem? _item;
}
