using System.Text;

namespace Arisoul.T212.Models;

public class CursorLimitTickerRequestParams : CursorLimitRequestParams
{
    public string? Ticker { get; set; }

    public override string ToString()
    {
        StringBuilder sb = new();

        sb.Append(base.ToString());

        if (Ticker is not null)
            sb.Append($"&ticker={Ticker}");

        return sb.ToString();
    }
}
