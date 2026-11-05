namespace Atc.Desktop.WebHost.Windows;

/// <content>
/// Window placement: which display, fullscreen without a frame, shown in the taskbar or not.
/// Fullscreen uses the standard switch: remember the window with GetWindowPlacement, drop the frame, cover the
/// display; to leave, put the frame back and restore the remembered placement (so a maximized window comes back
/// maximized).
/// </content>
internal sealed partial class Win32Window
{
    private readonly FullscreenState fullscreen = new();
    private WindowPlacement activePlacement;
    private WindowPlacementData? savedPlacement;

    public WindowPlacement Placement
        => activePlacement;

    public void SetPlacement(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (placement.ShowInTaskbar != activePlacement.ShowInTaskbar)
        {
            SetShowInTaskbar(placement.ShowInTaskbar);
        }

        if (placement.Fullscreen)
        {
            EnterFullscreen(placement.Monitor);
        }
        else
        {
            if (fullscreen.IsFullscreen)
            {
                LeaveFullscreen();
            }

            if (placement.Monitor is not null && placement.Monitor != activePlacement.Monitor)
            {
                MoveToMonitor(placement.Monitor);
            }
        }

        fullscreen.SetByWindow(placement.Fullscreen);
        Update(placement);
    }

    /// <summary>F11.</summary>
    private void ToggleFullscreen()
    {
        var hadFullscreenElement = webView?.ContainsFullScreenElement == true;
        if (fullscreen.Toggle() == FullscreenChange.Enter)
        {
            EnterFullscreen(activePlacement.Monitor);
        }
        else
        {
            LeaveFullscreen();
            if (hadFullscreenElement)
            {
                _ = webView!.ExecuteScriptAsync("document.exitFullscreen()");
            }
        }

        UpdateFullscreen();
    }

    /// <summary>A video (or any element) in the page went into or out of fullscreen.</summary>
    private void OnFullscreenElementChanged()
    {
        switch (fullscreen.OnElement(webView?.ContainsFullScreenElement == true))
        {
            case FullscreenChange.Enter:
                EnterFullscreen(activePlacement.Monitor);
                break;
            case FullscreenChange.Leave:
                LeaveFullscreen();
                break;
            default:
                return;
        }

        UpdateFullscreen();
    }

    /// <summary>Before the window is shown. The default placement keeps the original code path.</summary>
    private void PlaceInitially()
    {
        fullscreen.SetByWindow(activePlacement.Fullscreen);
        if (activePlacement.IsDefault)
        {
            PlaceCentered();
            return;
        }

        if (activePlacement.Fullscreen && FindMonitor(activePlacement.Monitor) is { } target)
        {
            // Created without a frame (WS_POPUP), so only the bounds are needed.
            SetBounds(target.Bounds);
            return;
        }

        MoveToMonitor(activePlacement.Monitor);
    }

    /// <summary>A fullscreen window keeps covering its display when the DPI changes.</summary>
    private bool KeepFullscreenOnDpiChange()
    {
        if (!fullscreen.IsFullscreen || Win32Monitors.FromWindow(hwnd) is not { } monitor)
        {
            return false;
        }

        SetBounds(monitor.Bounds);
        return true;
    }

    private void EnterFullscreen(int? monitor)
    {
        if (FindMonitor(monitor) is not { } target)
        {
            return;
        }

        var style = GetStyle();
        if ((style & NativeMethods.WsOverlappedWindow) != 0)
        {
            // Windowed now: remember where it is, then drop the frame.
            var saved = new WindowPlacementData { Length = (uint)Marshal.SizeOf<WindowPlacementData>() };
            savedPlacement = NativeMethods.GetWindowPlacement(hwnd, ref saved) ? saved : null;
            SetStyle(style & ~NativeMethods.WsOverlappedWindow);
        }

        SetBounds(target.Bounds);
    }

    private void LeaveFullscreen()
    {
        SetStyle((GetStyle() & ~NativeMethods.WsPopup) | NativeMethods.WsOverlappedWindow);
        if (savedPlacement is { } saved)
        {
            NativeMethods.SetWindowPlacement(hwnd, saved);
            savedPlacement = null;
        }
        else
        {
            // Started fullscreen: there is no earlier window to go back to.
            PlaceCentered();
        }

        NativeMethods.SetWindowPos(
            hwnd,
            insertAfter: 0,
            x: 0,
            y: 0,
            width: 0,
            height: 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
    }

    private void MoveToMonitor(int? monitor)
    {
        if (FindMonitor(monitor) is not { } target)
        {
            PlaceCentered();
            return;
        }

        // Move onto the display first, so the size below is computed with that display's DPI.
        var work = target.WorkArea;
        NativeMethods.SetWindowPos(
            hwnd,
            insertAfter: 0,
            work.X,
            work.Y,
            width: 0,
            height: 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        PlaceCentered();
    }

    private void SetShowInTaskbar(bool show)
    {
        // The taskbar only notices a changed tool-window style when the window is shown again.
        NativeMethods.ShowWindow(hwnd, NativeMethods.SwHide);
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle);
        exStyle = show
            ? exStyle & ~(nint)NativeMethods.WsExToolWindow
            : exStyle | (nint)NativeMethods.WsExToolWindow;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, exStyle);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SwShow);
    }

    private HostMonitor? FindMonitor(int? monitor)
        => monitor is null
            ? Win32Monitors.FromWindow(hwnd)
            : MonitorLayout.Find(Win32Monitors.GetAll(), monitor);

    private void SetBounds(HostRect bounds)
        => NativeMethods.SetWindowPos(
            hwnd,
            insertAfter: 0,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);

    private uint GetStyle()
        => unchecked((uint)(long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle));

    private void SetStyle(uint style)
        => NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlStyle, unchecked((int)style));

    private void UpdateFullscreen()
        => Update(new WindowPlacement
        {
            Monitor = activePlacement.Monitor,
            Fullscreen = fullscreen.IsFullscreen,
            ShowInTaskbar = activePlacement.ShowInTaskbar,
        });

    private void Update(WindowPlacement placement)
    {
        activePlacement = placement;
        events.OnPlacementChanged(placement);
    }
}