namespace Atc.Desktop.WebHost.Linux;

/// <summary>
/// The Linux window: GTK4 + WebKitGTK 6.0 through GirCore (C# bindings, no C/C++ to build).
/// Needs the system packages <c>libgtk-4-1</c> and <c>libwebkitgtk-6.0-4</c>.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class WebKitGtkPlatform : IHostPlatform
{
    /// <inheritdoc />
    public string Name
        => "webkitgtk";

    /// <inheritdoc />
    public string Origin
        => GtkHostWindow.Origin;

    /// <inheritdoc />
    public bool IsSupported
        => OperatingSystem.IsLinux();

    /// <inheritdoc />
    public bool IsSystemDarkTheme()
    {
        // GTK_THEME=Adwaita:dark is the common explicit switch. Reading the desktop's colour scheme
        // (org.gnome.desktop.interface color-scheme) needs a settings schema that may not exist.
        var theme = Environment.GetEnvironmentVariable("GTK_THEME");
        return theme is not null && theme.EndsWith(":dark", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public void AllowActivationHandOff()
    {
        // Nothing needed: the running instance presents its window itself.
    }

    /// <inheritdoc />
    public int Run(
        HostWindowOptions options,
        IHostWindowEvents events)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(events);
        return GtkHostWindow.Run(options, events);
    }
}