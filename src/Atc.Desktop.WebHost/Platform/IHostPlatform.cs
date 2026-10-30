namespace Atc.Desktop.WebHost.Platform;

/// <summary>
/// A native window + webview implementation: WebView2 on Windows (Atc.Desktop.WebHost.Windows),
/// WebKitGTK on Linux (Atc.Desktop.WebHost.Linux).
/// </summary>
public interface IHostPlatform
{
    /// <summary>Gets a short name, e.g. "webview2" or "webkitgtk". The page sees it as <c>atcHost.platform</c>.</summary>
    string Name { get; }

    /// <summary>Gets the origin the page is served from, e.g. "https://app.atc-webhost.example". The page sees it as <c>atcHost.boot.origin</c>.</summary>
    string Origin { get; }

    /// <summary>Gets a value indicating whether this platform works on the current OS.</summary>
    bool IsSupported { get; }

    /// <summary>Reads whether the OS uses a dark app theme. Called before the window is created.</summary>
    /// <returns><see langword="true"/> for dark.</returns>
    bool IsSystemDarkTheme();

    /// <summary>
    /// Called in a second instance just before it hands its files to the running one, so the running
    /// window may come to the front (Windows only allows that when the sender permits it).
    /// </summary>
    void AllowActivationHandOff();

    /// <summary>Creates the window and runs its message loop until it closes. Blocks the calling thread.</summary>
    /// <param name="options">What to show.</param>
    /// <param name="events">Callbacks into the host. All are called on the UI thread.</param>
    /// <returns>The process exit code.</returns>
    int Run(
        HostWindowOptions options,
        IHostWindowEvents events);
}