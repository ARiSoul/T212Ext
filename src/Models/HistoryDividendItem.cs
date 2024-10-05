using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class HistoryDividendItem
{
    /// <summary>
    /// Gets or Sets Type
    /// </summary>
    [DataMember(Name = "type", EmitDefaultValue = false)]
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// In instrument currency
    /// </summary>
    /// <value>In instrument currency</value>
    [DataMember(Name = "amount", EmitDefaultValue = false)]
    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or Sets AmountInEuro
    /// </summary>
    [DataMember(Name = "amountInEuro", EmitDefaultValue = false)]
    [JsonPropertyName("amountInEuro")]
    public decimal? AmountInEuro { get; set; }

    /// <summary>
    /// In instrument currency
    /// </summary>
    /// <value>In instrument currency</value>
    [DataMember(Name = "grossAmountPerShare", EmitDefaultValue = false)]
    [JsonPropertyName("grossAmountPerShare")]
    public decimal? GrossAmountPerShare { get; set; }

    /// <summary>
    /// Gets or Sets PaidOn
    /// </summary>
    [DataMember(Name = "paidOn", EmitDefaultValue = false)]
    [JsonPropertyName("paidOn")]
    public DateTime? PaidOn { get; set; }

    /// <summary>
    /// Gets or Sets Quantity
    /// </summary>
    [DataMember(Name = "quantity", EmitDefaultValue = false)]
    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Gets or Sets Reference
    /// </summary>
    [DataMember(Name = "reference", EmitDefaultValue = false)]
    [JsonPropertyName("reference")]
    public string Reference { get; set; }

    /// <summary>
    /// Gets or Sets Ticker
    /// </summary>
    [DataMember(Name = "ticker", EmitDefaultValue = false)]
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; }
}