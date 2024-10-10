using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class HistoryOrder
{
    /// <summary>
    /// Gets or Sets DateCreated
    /// </summary>
    [DataMember(Name = "dateCreated", EmitDefaultValue = false)]
    [JsonPropertyName("dateCreated")]
    public DateTime? DateCreated { get; set; }

    /// <summary>
    /// Gets or Sets DateExecuted
    /// </summary>
    [DataMember(Name = "dateExecuted", EmitDefaultValue = false)]
    [JsonPropertyName("dateExecuted")]
    public DateTime? DateExecuted { get; set; }

    /// <summary>
    /// Gets or Sets DateModified
    /// </summary>
    [DataMember(Name = "dateModified", EmitDefaultValue = false)]
    [JsonPropertyName("dateModified")]
    public DateTime? DateModified { get; set; }


    /// <summary>
    /// In the instrument currency
    /// </summary>
    /// <value>In the instrument currency</value>
    [DataMember(Name = "fillCost", EmitDefaultValue = false)]
    [JsonPropertyName("fillCost")]
    public decimal? FillCost { get; set; }

    /// <summary>
    /// Gets or Sets FillId
    /// </summary>
    [DataMember(Name = "fillId", EmitDefaultValue = false)]
    [JsonPropertyName("fillId")]
    public long? FillId { get; set; }

    /// <summary>
    /// In the instrument currency
    /// </summary>
    /// <value>In the instrument currency</value>
    [DataMember(Name = "fillPrice", EmitDefaultValue = false)]
    [JsonPropertyName("fillPrice")]
    public decimal? FillPrice { get; set; }

    /// <summary>
    /// Gets or Sets FillResult
    /// </summary>
    [DataMember(Name = "fillResult", EmitDefaultValue = false)]
    [JsonPropertyName("fillResult")]
    public decimal? FillResult { get; set; }


    /// <summary>
    /// Applicable to quantity orders
    /// </summary>
    /// <value>Applicable to quantity orders</value>
    [DataMember(Name = "filledQuantity", EmitDefaultValue = false)]
    [JsonPropertyName("filledQuantity")]
    public decimal? FilledQuantity { get; set; }

    /// <summary>
    /// Applicable to value orders
    /// </summary>
    /// <value>Applicable to value orders</value>
    [DataMember(Name = "filledValue", EmitDefaultValue = false)]
    [JsonPropertyName("filledValue")]
    public decimal? FilledValue { get; set; }

    /// <summary>
    /// Gets or Sets Id
    /// </summary>
    [DataMember(Name = "id", EmitDefaultValue = false)]
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>
    /// Applicable to limit orders
    /// </summary>
    /// <value>Applicable to limit orders</value>
    [DataMember(Name = "limitPrice", EmitDefaultValue = false)]
    [JsonPropertyName("limitPrice")]
    public decimal? LimitPrice { get; set; }

    /// <summary>
    /// Applicable to quantity orders
    /// </summary>
    /// <value>Applicable to quantity orders</value>
    [DataMember(Name = "orderedQuantity", EmitDefaultValue = false)]
    [JsonPropertyName("orderedQuantity")]
    public decimal? OrderedQuantity { get; set; }

    /// <summary>
    /// Applicable to value orders
    /// </summary>
    /// <value>Applicable to value orders</value>
    [DataMember(Name = "orderedValue", EmitDefaultValue = false)]
    [JsonPropertyName("orderedValue")]
    public decimal? OrderedValue { get; set; }

    /// <summary>
    /// Gets or Sets ParentOrder
    /// </summary>
    [DataMember(Name = "parentOrder", EmitDefaultValue = false)]
    [JsonPropertyName("parentOrder")]
    public long? ParentOrder { get; set; }


    /// <summary>
    /// Applicable to stop orders
    /// </summary>
    /// <value>Applicable to stop orders</value>
    [DataMember(Name = "stopPrice", EmitDefaultValue = false)]
    [JsonPropertyName("stopPrice")]
    public decimal? StopPrice { get; set; }

    /// <summary>
    /// Gets or Sets Ticker
    /// </summary>
    [DataMember(Name = "ticker", EmitDefaultValue = false)]
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; }

    /// <summary>
    /// Gets or Sets Executor
    /// </summary>
    [DataMember(Name = "executor", EmitDefaultValue = false)]
    [JsonPropertyName("executor")]
    public string? Executor { get; set; }

    /// <summary>
    /// Gets or Sets FillType
    /// </summary>
    [DataMember(Name = "fillType", EmitDefaultValue = false)]
    [JsonPropertyName("fillType")]
    public string? FillType { get; set; }

    /// <summary>
    /// Gets or Sets Status
    /// </summary>
    [DataMember(Name = "status", EmitDefaultValue = false)]
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or Sets Type
    /// </summary>
    [DataMember(Name = "type", EmitDefaultValue = false)]
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}