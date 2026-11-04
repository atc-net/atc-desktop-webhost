namespace Atc.Desktop.WebHost.Windows;

/// <summary>The displays, in physical pixels. Works before the window exists.</summary>
[SupportedOSPlatform("windows")]
internal static class Win32Monitors
{
    /// <summary>Lists the displays, numbered left to right.</summary>
    /// <returns>The displays.</returns>
    public static unsafe IReadOnlyList<HostMonitor> GetAll()
    {
        // Without per-monitor DPI awareness, Windows reports scaled (virtual) coordinates.
        // The window sets the same context when it starts; setting it twice is harmless.
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorAwareV2);

        var found = new List<HostMonitor>();
        var handle = GCHandle.Alloc(found);
        try
        {
            NativeMethods.EnumDisplayMonitors(0, 0, &OnMonitor, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return MonitorLayout.Number(found);
    }

    /// <summary>The display a window is on (most of it).</summary>
    /// <param name="hwnd">The window.</param>
    /// <returns>The display, or <see langword="null"/>.</returns>
    public static HostMonitor? FromWindow(nint hwnd)
        => Describe(NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest));

    [UnmanagedCallersOnly]
    private static int OnMonitor(
        nint monitor,
        nint hdc,
        nint clip,
        nint data)
    {
        if (GCHandle.FromIntPtr(data).Target is List<HostMonitor> list && Describe(monitor) is { } found)
        {
            list.Add(found);
        }

        return 1;
    }

    private static HostMonitor? Describe(nint monitor)
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfoW(monitor, ref info))
        {
            return null;
        }

        var scale = NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MdtEffectiveDpi, out var dpi, out _) == 0
            ? dpi / 96.0
            : 1.0;
        return new HostMonitor(
            Number: 0,
            ToRect(info.Monitor),
            ToRect(info.Work),
            (info.Flags & NativeMethods.MonitorInfoPrimary) != 0,
            scale);
    }

    private static HostRect ToRect(Rect rect)
        => new(rect.Left, rect.Top, rect.Width, rect.Height);
}