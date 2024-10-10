using SQLite;

namespace Arisoul.T212.Models;

public class Parameter
{
    [PrimaryKey]
    public string Key { get; set; }
    public string? Value { get; set; }
}
