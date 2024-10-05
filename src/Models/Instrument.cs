using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class Instrument
{
    /// <summary>
    /// Gets or Sets Type
    /// </summary>
    [DataMember(Name = "type", EmitDefaultValue = false)]
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// On the platform since
    /// </summary>
    /// <value>On the platform since</value>
    [DataMember(Name = "addedOn", EmitDefaultValue = false)]
    [JsonPropertyName("addedOn")]
    public DateTime? AddedOn { get; set; }

    /// <summary>
    /// ISO 4217
    /// </summary>
    /// <value>ISO 4217</value>
    [DataMember(Name = "currencyCode", EmitDefaultValue = false)]
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; }

    /// <summary>
    /// Gets or Sets Isin
    /// </summary>
    [DataMember(Name = "isin", EmitDefaultValue = false)]
    [JsonPropertyName("isin")]
    public string Isin { get; set; }

    /// <summary>
    /// Gets or Sets MaxOpenQuantity
    /// </summary>
    [DataMember(Name = "maxOpenQuantity", EmitDefaultValue = false)]
    [JsonPropertyName("maxOpenQuantity")]
    public decimal? MaxOpenQuantity { get; set; }

    /// <summary>
    /// A single order must be equal to or exceed this value
    /// </summary>
    /// <value>A single order must be equal to or exceed this value</value>
    [DataMember(Name = "minTradeQuantity", EmitDefaultValue = false)]
    [JsonPropertyName("minTradeQuantity")]
    public decimal? MinTradeQuantity { get; set; }

    /// <summary>
    /// Gets or Sets Name
    /// </summary>
    [DataMember(Name = "name", EmitDefaultValue = false)]
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or Sets Shortname
    /// </summary>
    [DataMember(Name = "shortname", EmitDefaultValue = false)]
    [JsonPropertyName("shortname")]
    public string Shortname { get; set; }

    /// <summary>
    /// Unique identifier
    /// </summary>
    /// <value>Unique identifier</value>
    [DataMember(Name = "ticker", EmitDefaultValue = false)]
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; }


    /// <summary>
    /// Get items in the /exchanges endpoint
    /// </summary>
    /// <value>Get items in the /exchanges endpoint</value>
    [DataMember(Name = "workingScheduleId", EmitDefaultValue = false)]
    [JsonPropertyName("workingScheduleId")]
    public long? WorkingScheduleId { get; set; }
}