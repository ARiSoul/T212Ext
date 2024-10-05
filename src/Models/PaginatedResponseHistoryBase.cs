using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public abstract class PaginatedResponseHistoryBase<T>
    where T : class
{
    /// <summary>
    /// Gets or Sets Items
    /// </summary>
    [DataMember(Name = "items", EmitDefaultValue = false)]
    [JsonPropertyName("items")]
    public IEnumerable<T> Items { get; set; } = [];

    /// <summary>
    /// Gets or Sets NextPagePath
    /// </summary>
    [DataMember(Name = "nextPagePath", EmitDefaultValue = false)]
    [JsonPropertyName("nextPagePath")]
    public string NextPagePath { get; set; }
}
