using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class Position
{
    /// <summary>
    /// Origin
    /// </summary>
    /// <value>Origin</value>
    [DataMember(Name = "frontend", EmitDefaultValue = false)]
    [JsonPropertyName("frontend")]
    public FrontendEnum? Frontend { get; set; }

    /// <summary>
    /// Gets or Sets AveragePrice
    /// </summary>
    [DataMember(Name = "averagePrice", EmitDefaultValue = false)]
    [JsonPropertyName("averagePrice")]
    public decimal? AveragePrice { get; set; }

    /// <summary>
    /// Gets or Sets CurrentPrice
    /// </summary>
    [DataMember(Name = "currentPrice", EmitDefaultValue = false)]
    [JsonPropertyName("currentPrice")]
    public decimal? CurrentPrice { get; set; }

    /// <summary>
    /// Forex movement impact, only applies to positions with instrument currency that differs from the accounts&#x27;
    /// </summary>
    /// <value>Forex movement impact, only applies to positions with instrument currency that differs from the accounts&#x27;</value>
    [DataMember(Name = "fxPpl", EmitDefaultValue = false)]
    [JsonPropertyName("fxPpl")]
    public decimal? FxPpl { get; set; }

    /// <summary>
    /// Gets or Sets InitialFillDate
    /// </summary>
    [DataMember(Name = "initialFillDate", EmitDefaultValue = false)]
    [JsonPropertyName("initialFillDate")]
    public DateTime? InitialFillDate { get; set; }

    /// <summary>
    /// Additional quantity that can be bought
    /// </summary>
    /// <value>Additional quantity that can be bought</value>
    [DataMember(Name = "maxBuy", EmitDefaultValue = false)]
    [JsonPropertyName("maxBuy")]
    public decimal? MaxBuy { get; set; }

    /// <summary>
    /// Quantity that can be sold
    /// </summary>
    /// <value>Quantity that can be sold</value>
    [DataMember(Name = "maxSell", EmitDefaultValue = false)]
    [JsonPropertyName("maxSell")]
    public decimal? MaxSell { get; set; }

    /// <summary>
    /// Invested in pies
    /// </summary>
    /// <value>Invested in pies</value>
    [DataMember(Name = "pieQuantity", EmitDefaultValue = false)]
    [JsonPropertyName("pieQuantity")]
    public decimal? PieQuantity { get; set; }

    /// <summary>
    /// Gets or Sets Ppl
    /// </summary>
    [DataMember(Name = "ppl", EmitDefaultValue = false)]
    [JsonPropertyName("ppl")]
    public decimal? Ppl { get; set; }

    /// <summary>
    /// Gets or Sets Quantity
    /// </summary>
    [DataMember(Name = "quantity", EmitDefaultValue = false)]
    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Unique instrument identifier
    /// </summary>
    /// <value>Unique instrument identifier</value>
    [DataMember(Name = "ticker", EmitDefaultValue = false)]
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontendEnum
{
    /// <summary>
    /// Enum API for value: API
    /// </summary>
    [EnumMember(Value = "API")]
    API = 1,
    /// <summary>
    /// Enum IOS for value: IOS
    /// </summary>
    [EnumMember(Value = "IOS")]
    IOS = 2,
    /// <summary>
    /// Enum ANDROID for value: ANDROID
    /// </summary>
    [EnumMember(Value = "ANDROID")]
    ANDROID = 3,
    /// <summary>
    /// Enum WEB for value: WEB
    /// </summary>
    [EnumMember(Value = "WEB")]
    WEB = 4,
    /// <summary>
    /// Enum SYSTEM for value: SYSTEM
    /// </summary>
    [EnumMember(Value = "SYSTEM")]
    SYSTEM = 5,
    /// <summary>
    /// Enum AUTOINVEST for value: AUTOINVEST
    /// </summary>
    [EnumMember(Value = "AUTOINVEST")]
    AUTOINVEST = 6
}