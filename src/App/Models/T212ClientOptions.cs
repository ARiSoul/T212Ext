using Arisoul.T212.Client;

namespace Arisoul.T212.App.Models;

public partial class T212ClientOptions 
    : ObservableValidator, IT212ClientOptions
{
    [ObservableProperty]
    string _baseAddress = "https://demo.trading212.com";

    [ObservableProperty]
    string _apiKey = "demo";

    [ObservableProperty]
    string _apiVersion = "0";
    
    public bool IsInitiated { get; set; } = false;
    public bool HasChanged { get; private set; } = false;

    public void LoadOptions()
    {
        var optionsSerialized = Preferences.Get("T212ClientOptions", string.Empty);

        if (!string.IsNullOrWhiteSpace(optionsSerialized))
        {
            var options = JsonSerializer.Deserialize<T212ClientOptions>(optionsSerialized) 
                ?? throw new InvalidOperationException("Failed to deserialize T212ClientOptions.");
            
            this.BaseAddress = options.BaseAddress;
            this.ApiKey = options.ApiKey;
            this.ApiVersion = options.ApiVersion;
            this.IsInitiated = options.IsInitiated;
            this.HasChanged = options.HasChanged;
        }
    }

    public void SaveOptions(bool markAsChanged)
    {
        HasChanged = markAsChanged;
        var optionsSerialized = JsonSerializer.Serialize(this);
        Preferences.Set("T212ClientOptions", optionsSerialized);
    }
}
