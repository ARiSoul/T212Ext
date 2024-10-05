using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class PaginatedResponseHistoryDividendItem
{
    /// <summary>
    /// Gets or Sets Items
    /// </summary>
    [DataMember(Name = "items", EmitDefaultValue = false)]
    [JsonPropertyName("items")]
    public IEnumerable<HistoryDividendItem> Items { get; set; } = [];

    /// <summary>
    /// Gets or Sets NextPagePath
    /// </summary>
    [DataMember(Name = "nextPagePath", EmitDefaultValue = false)]
    [JsonPropertyName("nextPagePath")]
    public string? NextPagePath { get; set; }
}
