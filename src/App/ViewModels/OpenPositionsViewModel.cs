namespace Arisoul.T212.App.ViewModels;

public partial class OpenPositionsViewModel : BaseViewModel
{
    // TODO: Replace this with your own data service
    readonly SampleDataService _dataService;

	[ObservableProperty]
	bool _isRefreshing;

	[ObservableProperty]
	ObservableCollection<SampleItem>? _items;

	public OpenPositionsViewModel(SampleDataService service)
	{
		_dataService = service;
	}

	[RelayCommand]
	private async Task OnRefreshing()
	{
		IsRefreshing = true;

		try
		{
			await LoadDataAsync();
		}
		finally
		{
			IsRefreshing = false;
		}
	}

	[RelayCommand]
	public async Task LoadMore()
	{
		if (Items is null)
		{
			return;
		}

		var moreItems = await _dataService.GetItems();

		foreach (var item in moreItems)
		{
			Items.Add(item);
		}
	}

	public async Task LoadDataAsync()
	{
		Items = new ObservableCollection<SampleItem>(await _dataService.GetItems());
	}

	[RelayCommand]
	private async Task GoToDetails(SampleItem item)
	{
		await Shell.Current.GoToAsync(nameof(OpenPositionsDetailPage), true, new Dictionary<string, object>
		{
			{ "Item", item }
		});
	}
}
