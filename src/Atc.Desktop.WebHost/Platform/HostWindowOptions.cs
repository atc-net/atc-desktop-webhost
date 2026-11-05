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

    /// <summary>Gets where the window opens. Applied before the window is shown, so it doesn't jump.</summary>
    public WindowPlacement Placement { get; init; } = WindowPlacement.Default;

    /// <summary>Gets the standard keys the window handles before the page. See <see cref="HostKeys"/>.</summary>
    public HostKeys Keys { get; init; } = HostKeys.All;

    /// <summary>Gets the UI language, e.g. "da-DK": for the webview's own menus and dialogs and the host's messages.</summary>
    public string Language { get; init; } = "en";

    /// <summary>Gets what the right-click menu shows (<see cref="HostContextMenu.Full"/> when dev tools are on).</summary>
    public HostContextMenu ContextMenu { get; init; } = HostContextMenu.Edit;

    /// <summary>Gets the extra file folders served on the files origin. See <see cref="FrontendMount"/>.</summary>
    public IReadOnlyList<FrontendMount> Mounts { get; init; } = [];
}