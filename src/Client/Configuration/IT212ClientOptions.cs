namespace Arisoul.T212.Client;

public interface IT212ClientOptions
{
    string BaseAddress { get; set; }
    string ApiKey { get; set; }
    string ApiVersion { get; set; }

    void LoadOptions();
    void SaveOptions();
}
