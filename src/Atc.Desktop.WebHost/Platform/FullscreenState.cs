namespace Atc.Desktop.WebHost.Platform;

/// <summary>
/// Who made the window fullscreen, so the right event ends it. F11 (or the app) and the page's own fullscreen
/// element (a video's fullscreen button) both use the window's fullscreen:
/// <list type="bullet">
/// <item>F11 always toggles. Leaving also ends a fullscreen element.</item>
/// <item>A fullscreen element enters fullscreen only when the window isn't already; when it ends, the window
/// leaves fullscreen only if the element started it.</item>
/// </list>
/// Used on the UI thread only.
/// </summary>
internal sealed class FullscreenState
{
    private enum Source
    {
        None,
        Window,
        Element,
    }

    private Source source;

    /// <summary>Gets a value indicating whether the window is fullscreen.</summary>
    public bool IsFullscreen
        => source != Source.None;

    /// <summary>The app or the user (F11, <c>window.setPlacement</c>, the start-up placement) set fullscreen on or off.</summary>
    /// <param name="fullscreen">The new state.</param>
    public void SetByWindow(bool fullscreen)
        => source = fullscreen ? Source.Window : Source.None;

    /// <summary>F11 was pressed.</summary>
    /// <returns><see cref="FullscreenChange.Enter"/> or <see cref="FullscreenChange.Leave"/>.</returns>
    public FullscreenChange Toggle()
    {
        var change = IsFullscreen ? FullscreenChange.Leave : FullscreenChange.Enter;
        source = IsFullscreen ? Source.None : Source.Window;
        return change;
    }

    /// <summary>The page's fullscreen element appeared or went away.</summary>
    /// <param name="containsFullscreenElement">Whether the page now has a fullscreen element.</param>
    /// <returns>What to do with the window.</returns>
    public FullscreenChange OnElement(bool containsFullscreenElement)
    {
        if (containsFullscreenElement && source == Source.None)
        {
            source = Source.Element;
            return FullscreenChange.Enter;
        }

        if (!containsFullscreenElement && source == Source.Element)
        {
            source = Source.None;
            return FullscreenChange.Leave;
        }

        return FullscreenChange.None;
    }
}