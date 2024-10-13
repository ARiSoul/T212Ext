using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class DividendModel
    : HistoryDividendItem
{
    [JsonPropertyName("instrument")]
    public Instrument Instrument { get; set; } = new();

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; }
}
