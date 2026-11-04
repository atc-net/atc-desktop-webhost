namespace Atc.Desktop.WebHost.Platform;

/// <summary>
/// Where the window opens: which display, fullscreen or not, and whether it shows in the taskbar.
/// The default leaves the window where the OS puts it, centred, with a normal frame.
/// </summary>
public sealed class WindowPlacement
{
    /// <summary>Gets the default placement: centred, normal frame, in the taskbar.</summary>
    public static WindowPlacement Default { get; } = new();

    /// <summary>
    /// Gets the display number from <see cref="IHostPlatform.GetMonitors"/> (1 = the leftmost), or <see langword="null"/>
    /// for the display the OS picks. An unknown number falls back to the primary display.
    /// </summary>
    public int? Monitor { get; init; }

    /// <summary>
    /// Gets a value indicating whether the window has no frame and fills the whole display (kiosk, a wall or LED display).
    /// </summary>
    public bool Fullscreen { get; init; }

    /// <summary>
    /// Gets a value indicating whether the window shows in the taskbar and Alt+Tab. Windows only; set it at start,
    /// changing it later hides and shows the window.
    /// </summary>
    public bool ShowInTaskbar { get; init; } = true;

    /// <summary>Gets a value indicating whether this is the default placement.</summary>
    public bool IsDefault
        => Monitor is null && !Fullscreen && ShowInTaskbar;
}