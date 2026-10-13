using Microsoft.JSInterop;

namespace Arisoul.T212Ext.Web.Services;

public enum ThemeChoice
{
    Auto,
    Light,
    Dark,
}

/// <summary>
/// The colour theme of one browser tab. The choice is kept in the browser (localStorage) and applied
/// before the first paint by a script in <c>App.razor</c>; this service keeps .NET in step with it,
/// so charts can pick their light or dark colours.
/// </summary>
public sealed class ThemeService(IJSRuntime js) : IAsyncDisposable
{
    private DotNetObjectReference<ThemeService>? _reference;

    public ThemeChoice Choice { get; private set; } = ThemeChoice.Auto;

    /// <summary>The theme in effect (with Auto, the system's).</summary>
    public bool IsDark { get; private set; }

    /// <summary>False until the browser has told us the theme; charts wait for it.</summary>
    public bool IsReady { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (IsReady)
            return;

        var stored = await js.InvokeAsync<string?>("t212ext.theme.get");
        Choice = Enum.TryParse<ThemeChoice>(stored, ignoreCase: true, out var choice) ? choice : ThemeChoice.Auto;
        IsDark = await js.InvokeAsync<bool>("t212ext.theme.apply", Choice.ToString().ToLowerInvariant());

        _reference = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("t212ext.theme.watch", _reference);

        IsReady = true;
        Changed?.Invoke();
    }

    public async Task SetAsync(ThemeChoice choice)
    {
        Choice = choice;
        IsDark = await js.InvokeAsync<bool>("t212ext.theme.set", choice.ToString().ToLowerInvariant());
        Changed?.Invoke();
    }

    [JSInvokable]
    public void OnSystemThemeChanged(bool dark)
    {
        IsDark = dark;
        Changed?.Invoke();
    }

    public ValueTask DisposeAsync()
    {
        _reference?.Dispose();
        return ValueTask.CompletedTask;
    }
}
