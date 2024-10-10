using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class HistoryOrderModel : HistoryOrder
{
    /// <summary>
    /// Gets or Sets Taxes
    /// </summary>
    [DataMember(Name = "taxes", EmitDefaultValue = false)]
    [JsonPropertyName("taxes")]
    public List<Tax> Taxes { get; set; } = [];
}