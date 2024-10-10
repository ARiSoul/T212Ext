using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class Tax
{
    /// <summary>
    /// Gets or Sets Name
    /// </summary>
    [DataMember(Name = "name", EmitDefaultValue = false)]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or Sets FillId
    /// </summary>
    [DataMember(Name = "fillId", EmitDefaultValue = false)]
    [JsonPropertyName("fillId")]
    public string FillId { get; set; }


    /// <summary>
    /// Gets or Sets Quantity
    /// </summary>
    [DataMember(Name = "quantity", EmitDefaultValue = false)]
    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Gets or Sets TimeCharged
    /// </summary>
    [DataMember(Name = "timeCharged", EmitDefaultValue = false)]
    [JsonPropertyName("timeCharged")]
    public DateTime? TimeCharged { get; set; }

    [DataMember(Name = "orderId", EmitDefaultValue = false)]
    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }
}
