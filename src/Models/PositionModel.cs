using System.Text.Json.Serialization;

namespace Arisoul.T212.Models;

public class PositionModel : Position
{
    [JsonPropertyName("invested")]
    public decimal? Invested => Quantity * AveragePrice;

    [JsonPropertyName("instrument")]
    public Instrument Instrument { get; set; }

    [JsonPropertyName("dividends")]
    public List<HistoryDividendItem> Dividends { get; set; } = [];

    [JsonPropertyName("totalDividendsInEuro")]
    public decimal? TotalDividendsInEuro => Dividends.Sum(x => x.AmountInEuro);

    [JsonPropertyName("totalDividends")]
    public decimal? TotalDividends => Dividends.Sum(x => x.Amount);

    [JsonPropertyName("orders")]
    public List<HistoryOrderModel> Orders { get; set; } = [];
}
