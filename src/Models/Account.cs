using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class Account
{
    /// <summary>
    /// Gets or Sets Id
    /// </summary>
    [DataMember(Name = "id", EmitDefaultValue = false)]
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>
    /// ISO 4217
    /// </summary>
    /// <value>ISO 4217</value>
    [DataMember(Name = "currencyCode", EmitDefaultValue = false)]
    [JsonPropertyName("currencyCode")]
    [MaybeNull]
    public string CurrencyCode { get; set; }
}
