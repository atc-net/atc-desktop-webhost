namespace Atc.Desktop.WebHost.Platform;

/// <summary>What the platform window shows. Built by the host from <see cref="WebHostOptions"/>.</summary>
public sealed class HostWindowOptions
{
    /// <summary>Gets the app id, e.g. "AtcSoft.Markdown.Viewer".</summary>
    public required string AppId { get; init; }

    /// <summary>Gets the window title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the initial client width in device-independent pixels.</summary>
    public int Width { get; init; } = 1200;

    /// <summary>Gets the initial client height in device-independent pixels.</summary>
    public int Height { get; init; } = 800;

    /// <summary>Gets the colour painted before the page is ready, so there is no white flash in dark mode.</summary>
    public HostColor BackgroundColor { get; init; } = HostColor.White;

    /// <summary>Gets a value indicating whether the window uses dark chrome (title bar).</summary>
    public bool IsDark { get; init; }

    /// <summary>Gets the frontend files.</summary>
    public required FrontendSource Frontend { get; init; }

    /// <summary>Gets the page to open, relative to the frontend root.</summary>
    public string EntryPath { get; init; } = "index.html";

    /// <summary>Gets a folder the webview may use for its profile (cache, storage).</summary>
    public required string DataFolder { get; init; }

    /// <summary>Gets a value indicating whether dev tools and the default context menu are enabled.</summary>
    public bool DevToolsEnabled { get; init; }

    /// <summary>Gets extra command-line arguments for the browser engine (WebView2 only), or <see langword="null"/>.</summary>
    public string? AdditionalBrowserArguments { get; init; }

    /// <summary>Gets the path of a window icon (.ico on Windows, .png on Linux), or <see langword="null"/> for the exe icon.</summary>
    public string? IconPath { get; init; }
}