namespace Atc.Desktop.WebHost.Windows;

/// <content>Window placement: which display, fullscreen without a frame, shown in the taskbar or not.</content>
internal sealed partial class Win32Window
{
    private WindowPlacement activePlacement;

    public void SetPlacement(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        var taskbarChanged = placement.ShowInTaskbar != activePlacement.ShowInTaskbar;
        activePlacement = placement;

        var style = (placement.Fullscreen ? NativeMethods.WsPopup : NativeMethods.WsOverlappedWindow) | NativeMethods.WsVisible;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlStyle, unchecked((int)style));

        if (taskbarChanged)
        {
            // The taskbar only notices a changed tool-window style when the window is shown again.
            NativeMethods.ShowWindow(hwnd, NativeMethods.SwHide);
            var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle);
            exStyle = placement.ShowInTaskbar
                ? exStyle & ~(nint)NativeMethods.WsExToolWindow
                : exStyle | (nint)NativeMethods.WsExToolWindow;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, exStyle);
            NativeMethods.ShowWindow(hwnd, NativeMethods.SwShow);
        }

        ApplyPlacement();
    }

    /// <summary>Before the window is shown. The default placement keeps the original code path.</summary>
    private void PlaceInitially()
    {
        if (activePlacement.IsDefault)
        {
            PlaceCentered();
            return;
        }

        ApplyPlacement();
    }

    private void ApplyPlacement()
    {
        var target = activePlacement.Monitor is null
            ? Win32Monitors.FromWindow(hwnd)
            : MonitorLayout.Find(Win32Monitors.GetAll(), activePlacement.Monitor);
        if (target is null)
        {
            PlaceCentered();
            return;
        }

        if (activePlacement.Fullscreen)
        {
            var bounds = target.Bounds;
            NativeMethods.SetWindowPos(
                hwnd,
                insertAfter: 0,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
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
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
        PlaceCentered();
    }
}