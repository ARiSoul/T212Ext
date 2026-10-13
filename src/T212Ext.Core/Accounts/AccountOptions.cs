using System.ComponentModel.DataAnnotations;
using Arisoul.T212Ext.Client;

namespace Arisoul.T212Ext.Core.Accounts;

/// <summary>The <c>T212</c> configuration section.</summary>
public sealed class T212Options
{
    public const string Section = "T212";

    public List<AccountOptions> Accounts { get; set; } = [];
}

/// <summary>
/// One Trading 212 account. The key and secret come from user secrets (development) or
/// environment variables (server), never from appsettings.json in the repository.
/// An account without a key still works for CSV imports; the sync skips it.
/// Older keys have no secret: leave <see cref="ApiSecret"/> empty for them.
/// </summary>
public sealed class AccountOptions
{
    /// <summary>Stable id used in the database, e.g. "invest" or "isa". Lowercase letters, digits and dashes.</summary>
    [Required, RegularExpression("^[a-z0-9-]{1,50}$")]
    public string Key { get; set; } = "";

    [Required, MaxLength(300)]
    public string Name { get; set; } = "";

    public T212Environment Environment { get; set; } = T212Environment.Live;

    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";

    public T212Account ToT212Account() => new(Key, Environment, ApiKey, ApiSecret);
}
