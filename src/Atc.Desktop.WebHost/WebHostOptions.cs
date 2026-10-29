namespace Atc.Desktop.WebHost;

/// <summary>Settings for one desktop app.</summary>
public sealed class WebHostOptions
{
    /// <summary>Gets the stable app id, e.g. "AtcSoft.Markdown.Viewer". Used for single instance and folders.</summary>
    public required string AppId { get; init; }

    /// <summary>Gets the window title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the frontend files.</summary>
    public required FrontendSource Frontend { get; init; }

    /// <summary>Gets the native window implementation, e.g. <c>new WebView2Platform()</c> or <c>new WebKitGtkPlatform()</c>.</summary>
    public required IHostPlatform Platform { get; init; }

    /// <summary>Gets the vendor folder name below the user's app data folders.</summary>
    public string Vendor { get; init; } = "AtcSoft";

    /// <summary>
    /// Gets the function that turns a file path into the payload the page shows. Used for the file on
    /// the command line (cold start; it runs in parallel with the window start), files from a second
    /// instance (warm start) and the built-in <c>document.load</c> request. <see langword="null"/>: the
    /// host passes no documents.
    /// </summary>
    public DocumentLoader? LoadDocument { get; init; }

    /// <summary>Gets the page to open, relative to the frontend root.</summary>
    public string EntryPath { get; init; } = "index.html";

    /// <summary>Gets the initial client width in device-independent pixels.</summary>
    public int Width { get; init; } = 1200;

    /// <summary>Gets the initial client height in device-independent pixels.</summary>
    public int Height { get; init; } = 800;

    /// <summary>Gets the background painted before the page in light mode.</summary>
    public HostColor LightBackground { get; init; } = HostColor.White;

    /// <summary>Gets the background painted before the page in dark mode.</summary>
    public HostColor DarkBackground { get; init; } = HostColor.GitHubDark;

    /// <summary>Gets a value indicating whether dev tools and the default context menu are enabled. Also on with <c>--devtools</c>.</summary>
    public bool DevToolsEnabled { get; init; }

    /// <summary>Gets a value indicating whether a second launch hands its files to the running window.</summary>
    public bool SingleInstance { get; init; } = true;

    /// <summary>
    /// Gets how long to wait for the first document before the page loads without it. When the document
    /// is ready in time it is part of the boot data (no round trip); otherwise the page asks for it with
    /// <c>host.document</c> while it loads in parallel.
    /// </summary>
    public TimeSpan InlineDocumentWait { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Gets the quiet time before a file change is reported.</summary>
    public TimeSpan FileChangeDebounce { get; init; } = TimeSpan.FromMilliseconds(75);

    /// <summary>Gets the filters for the Open dialog, e.g. ("Markdown", ["*.md"]).</summary>
    public IReadOnlyList<FileDialogFilter> OpenFileFilters { get; init; } = [];

    /// <summary>Gets extra command-line arguments for the browser engine (WebView2 only).</summary>
    public string? AdditionalBrowserArguments { get; init; }

    /// <summary>Gets the path of a window icon, or <see langword="null"/> for the exe icon.</summary>
    public string? IconPath { get; init; }
}