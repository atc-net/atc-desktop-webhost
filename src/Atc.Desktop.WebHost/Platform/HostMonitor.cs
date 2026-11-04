namespace Atc.Desktop.WebHost.Platform;

/// <summary>A display, as the window can be placed on it.</summary>
/// <param name="Number">1, 2, … numbered left to right (then top to bottom), as people see them on the desk.</param>
/// <param name="Bounds">The whole display, in physical screen pixels.</param>
/// <param name="WorkArea">The display without the taskbar (the same as <paramref name="Bounds"/> where the OS doesn't say).</param>
/// <param name="IsPrimary">Whether this is the primary display.</param>
/// <param name="Scale">The display scale, e.g. 1.5 for 150 %.</param>
public sealed record HostMonitor(
    int Number,
    HostRect Bounds,
    HostRect WorkArea,
    bool IsPrimary,
    double Scale);