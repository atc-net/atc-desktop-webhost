namespace Atc.Desktop.WebHost.Windows;

/// <summary>
/// A top-level Win32 window with a WebView2 filling its client area. One per process.
/// The window shows immediately, painted with the background colour, while WebView2 starts.
/// Modelled on Photino.Native <c>Photino.Windows.cpp</c> (Apache-2.0), rewritten in C#: window class,
/// client-rect resize to <c>Bounds</c>, move notification, a posted message for UI-thread work.
/// Added here: per-monitor DPI v2 and <c>WM_DPICHANGED</c>, background brush from the theme, dark title bar.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class Win32Window : IHostWindow
{
    private const string ClassName = "AtcDesktopWebHost";
    private const uint WakeMessage = NativeMethods.WmApp + 1;

    private static Win32Window? current;

    private readonly HostWindowOptions options;
    private readonly IHostWindowEvents events;
    private nint hwnd;
    private nint backgroundBrush;
    private nint largeIcon;
    private nint smallIcon;
    private Win32SynchronizationContext? uiContext;
    private ExceptionDispatchInfo? fatalError;
    private bool isClosed;

    private Win32Window(
        HostWindowOptions options,
        IHostWindowEvents events)
    {
        this.options = options;
        this.events = events;
    }

    public static int Run(
        HostWindowOptions options,
        IHostWindowEvents events)
    {
        if (current is not null)
        {
            throw new InvalidOperationException("Only one window per process is supported.");
        }

        var window = new Win32Window(options, events);
        current = window;
        try
        {
            return window.RunMessageLoop();
        }
        finally
        {
            current = null;
        }
    }

    public void SetTitle(string title)
        => NativeMethods.SetWindowTextW(hwnd, title);

    public void BringToFront()
    {
        if (NativeMethods.IsIconic(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
        }

        NativeMethods.SetForegroundWindow(hwnd);
    }

    public void Close()
    {
        if (!isClosed)
        {
            NativeMethods.PostMessageW(hwnd, NativeMethods.WmClose, 0, 0);
        }
    }

    [UnmanagedCallersOnly]
    private static nint WindowProc(
        nint hwnd,
        uint message,
        nint wParam,
        nint lParam)
    {
        var window = current;
        if (window is null || window.hwnd != hwnd)
        {
            return NativeMethods.DefWindowProcW(hwnd, message, wParam, lParam);
        }

        try
        {
            return window.HandleMessage(message, wParam, lParam);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Exceptions must not cross the native boundary: stop the loop and rethrow after it.
            window.fatalError ??= ExceptionDispatchInfo.Capture(ex);
            NativeMethods.PostQuitMessage(1);
            return 0;
        }
    }

    private int RunMessageLoop()
    {
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorAwareV2);
        var instance = NativeMethods.GetModuleHandleW(null);
        RegisterWindowClass(instance);
        CreateNativeWindow(instance);

        uiContext = new Win32SynchronizationContext(hwnd, WakeMessage);
        SynchronizationContext.SetSynchronizationContext(uiContext);
        events.OnWindowCreated(this, uiContext);
        _ = InitializeWebViewAsync();

        while (NativeMethods.GetMessageW(out var message, 0, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(message);
            NativeMethods.DispatchMessageW(message);
        }

        ReleaseNativeResources();
        fatalError?.Throw();
        return 0;
    }

    private unsafe void RegisterWindowClass(nint instance)
    {
        var color = options.BackgroundColor;
        backgroundBrush = NativeMethods.CreateSolidBrush(color.R | ((uint)color.G << 8) | ((uint)color.B << 16));
        LoadIcons();

        fixed (char* className = ClassName)
        {
            var windowClass = new WndClassEx
            {
                Size = (uint)sizeof(WndClassEx),
                Style = NativeMethods.CsHRedraw | NativeMethods.CsVRedraw,
                WndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WindowProc,
                Instance = instance,
                Icon = largeIcon,
                SmallIcon = smallIcon,
                Cursor = NativeMethods.LoadCursorW(0, NativeMethods.IdcArrow),
                Background = backgroundBrush,
                ClassName = (nint)className,
            };
            NativeMethods.RegisterClassExW(windowClass);
        }
    }

    private void CreateNativeWindow(nint instance)
    {
        hwnd = NativeMethods.CreateWindowExW(
            exStyle: 0,
            ClassName,
            options.Title,
            NativeMethods.WsOverlappedWindow,
            NativeMethods.CwUseDefault,
            NativeMethods.CwUseDefault,
            NativeMethods.CwUseDefault,
            NativeMethods.CwUseDefault,
            parent: 0,
            menu: 0,
            instance,
            param: 0);
        if (hwnd == 0)
        {
            throw new InvalidOperationException($"CreateWindowEx failed (error {Marshal.GetLastPInvokeError()}).");
        }

        if (options.IsDark)
        {
            var on = 1;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
        }

        PlaceCentered();
        NativeMethods.ShowWindow(hwnd, NativeMethods.SwShowNormal);
        NativeMethods.UpdateWindow(hwnd);
    }

    private nint HandleMessage(
        uint message,
        nint wParam,
        nint lParam)
    {
        switch (message)
        {
            case WakeMessage:
                uiContext?.Drain();
                return 0;
            case NativeMethods.WmSize:
                OnResized(minimized: wParam == NativeMethods.SizeMinimized);
                return 0;
            case NativeMethods.WmMove:
                controller?.NotifyParentWindowPositionChanged();
                return 0;
            case NativeMethods.WmSetFocus:
                controller?.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
                return 0;
            case NativeMethods.WmDpiChanged:
                OnDpiChanged(lParam);
                return 0;
            case NativeMethods.WmDestroy:
                isClosed = true;
                events.OnClosed();
                controller?.Close();
                controller = null;
                NativeMethods.PostQuitMessage(0);
                return 0;
            default:
                return NativeMethods.DefWindowProcW(hwnd, message, wParam, lParam);
        }
    }

    private void PlaceCentered()
    {
        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        var scale = dpi / 96.0;
        var frame = new Rect
        {
            Right = (int)(options.Width * scale),
            Bottom = (int)(options.Height * scale),
        };
        NativeMethods.AdjustWindowRectExForDpi(ref frame, NativeMethods.WsOverlappedWindow, menu: false, exStyle: 0, dpi);

        var monitor = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        NativeMethods.GetMonitorInfoW(NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest), ref monitor);
        var work = monitor.Work;
        var width = Math.Min(frame.Width, work.Width);
        var height = Math.Min(frame.Height, work.Height);
        NativeMethods.SetWindowPos(
            hwnd,
            insertAfter: 0,
            work.Left + ((work.Width - width) / 2),
            work.Top + ((work.Height - height) / 2),
            width,
            height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    private unsafe void OnDpiChanged(nint lParam)
    {
        var suggested = *(Rect*)lParam;
        NativeMethods.SetWindowPos(
            hwnd,
            insertAfter: 0,
            suggested.Left,
            suggested.Top,
            suggested.Width,
            suggested.Height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    private void OnResized(bool minimized)
    {
        if (controller is null)
        {
            return;
        }

        // A hidden webview stops rendering, which saves CPU while minimized.
        controller.IsVisible = !minimized;
        if (!minimized && NativeMethods.GetClientRect(hwnd, out var client))
        {
            controller.Bounds = new Rectangle(0, 0, client.Width, client.Height);
        }
    }

    private void LoadIcons()
    {
        var file = options.IconPath ?? Environment.ProcessPath;
        if (file is not null)
        {
            NativeMethods.ExtractIconExW(file, 0, out largeIcon, out smallIcon, 1);
        }
    }

    private void ReleaseNativeResources()
    {
        if (backgroundBrush != 0)
        {
            NativeMethods.DeleteObject(backgroundBrush);
        }

        if (largeIcon != 0)
        {
            NativeMethods.DestroyIcon(largeIcon);
        }

        if (smallIcon != 0)
        {
            NativeMethods.DestroyIcon(smallIcon);
        }
    }

    private void ShowFatalError(Exception ex)
    {
        var text = ex is WebView2RuntimeNotFoundException
            ? "The Microsoft Edge WebView2 Runtime is not installed.\n\nInstall it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and start the app again."
            : "The window could not start:\n\n" + ex.Message;
        NativeMethods.MessageBoxW(hwnd, text, options.Title, NativeMethods.MbIconError);
        NativeMethods.DestroyWindow(hwnd);
    }
}