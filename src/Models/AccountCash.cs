using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class AccountCash
{
    /// <summary>
    /// Gets or Sets Blocked
    /// </summary>
    [DataMember(Name = "blocked", EmitDefaultValue = false)]
    [JsonPropertyName("blocked")]
    public decimal? Blocked { get; set; }

    /// <summary>
    /// Gets or Sets Free
    /// </summary>
    [DataMember(Name = "free", EmitDefaultValue = false)]
    [JsonPropertyName("free")]
    public decimal? Free { get; set; }

    /// <summary>
    /// Gets or Sets Invested
    /// </summary>
    [DataMember(Name = "invested", EmitDefaultValue = false)]
    [JsonPropertyName("invested")]
    public decimal? Invested { get; set; }

    /// <summary>
    /// Invested cash in pies
    /// </summary>
    /// <value>Invested cash in pies</value>
    [DataMember(Name = "pieCash", EmitDefaultValue = false)]
    [JsonPropertyName("pieCash")]
    public decimal? PieCash { get; set; }

    /// <summary>
    /// Gets or Sets Ppl
    /// </summary>
    [DataMember(Name = "ppl", EmitDefaultValue = false)]
    [JsonPropertyName("ppl")]
    public decimal? Ppl { get; set; }

    /// <summary>
    /// Gets or Sets Result
    /// </summary>
    [DataMember(Name = "result", EmitDefaultValue = false)]
    [JsonPropertyName("result")]
    public decimal? Result { get; set; }

    /// <summary>
    /// Gets or Sets Total
    /// </summary>
    [DataMember(Name = "total", EmitDefaultValue = false)]
    [JsonPropertyName("total")]
    public decimal? Total { get; set; }
}