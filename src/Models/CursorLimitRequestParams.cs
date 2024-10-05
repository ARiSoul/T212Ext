using System.Text;

namespace Arisoul.T212.Models;

public class CursorLimitRequestParams
{
    public string? Cursor { get; set; }
    public int Limit { get; set; } = 50;

    public override string ToString()
    {
        StringBuilder sb = new();

        sb.Append($"?limit={Limit}");

        if (Cursor is not null)
            sb.Append($"&cursor={Cursor}");

        return sb.ToString();
    }
}

