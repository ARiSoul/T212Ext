using System.Text.Json;
using Microsoft.JSInterop;

namespace Arisoul.T212Ext.Web.Services;

/// <summary>
/// Per-browser preferences (grid state, chosen account) in localStorage. Best effort: a browser that
/// refuses storage just forgets them. Only call after the first render (JS interop).
/// </summary>
public sealed class BrowserStorage(IJSRuntime js)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var text = await js.InvokeAsync<string?>("t212ext.storage.get", key);
            return text is null ? default : JsonSerializer.Deserialize<T>(text, Json);
        }
        catch (Exception ex) when (ex is JSException or JsonException or JSDisconnectedException or TaskCanceledException)
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        try
        {
            await js.InvokeVoidAsync("t212ext.storage.set", key, JsonSerializer.Serialize(value, Json));
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            // Not saved; nothing else depends on it.
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("t212ext.storage.remove", key);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            // Nothing to do.
        }
    }
}
