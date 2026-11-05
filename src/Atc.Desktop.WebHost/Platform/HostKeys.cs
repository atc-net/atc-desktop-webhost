namespace Atc.Desktop.WebHost.Platform;

/// <summary>Standard desktop keys the host handles before the page sees them. Combine flags; the default is <see cref="All"/>.</summary>
[Flags]
public enum HostKeys
{
    /// <summary>No keys: the page gets them all.</summary>
    None = 0,

    /// <summary>F11: fullscreen without a frame; F11 again brings the window back to its earlier size and place.</summary>
    Fullscreen = 1,

    /// <summary>Ctrl+F: the webview's find bar (Windows; on Linux the page gets the key).</summary>
    Find = 2,

    /// <summary>Ctrl+P: the print dialog.</summary>
    Print = 4,

    /// <summary>Ctrl+F4: close the window, like the title-bar button (asks the page first when the close guard is on).</summary>
    Close = 8,

    /// <summary>All standard keys.</summary>
    All = Fullscreen | Find | Print | Close,
}