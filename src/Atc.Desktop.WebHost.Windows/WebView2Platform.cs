namespace Atc.Desktop.WebHost.Windows;

/// <summary>
/// The Windows window: a plain Win32 window hosting WebView2. No WinForms, WPF or WinUI is loaded,
/// which keeps startup and install size down.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WebView2Platform : IHostPlatform
{
    /// <inheritdoc />
    public string Name
        => "webview2";

    /// <inheritdoc />
    public bool IsSupported
        => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763);

    /// <inheritdoc />
    public bool IsSystemDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    /// <inheritdoc />
    public void AllowActivationHandOff()
        => NativeMethods.AllowSetForegroundWindow(NativeMethods.AsfwAny);

    /// <inheritdoc />
    public int Run(
        HostWindowOptions options,
        IHostWindowEvents events)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(events);

        // WebView2 needs a single-threaded apartment. Use the calling thread when it is one
        // ([STAThread] Main); otherwise run the window on a new STA thread.
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return Win32Window.Run(options, events);
        }

        var exitCode = 0;
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                exitCode = Win32Window.Run(options, events);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                error = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            Name = "atc-webhost-ui",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        error?.Throw();
        return exitCode;
    }
}