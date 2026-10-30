namespace Atc.Desktop.WebHost.Linux;

/// <summary>
/// The Linux window: GTK4 + WebKitGTK 6.0 through GirCore (C# bindings, no C/C++ to build).
/// Needs the system packages <c>libgtk-4-1</c> and <c>libwebkitgtk-6.0-4</c>.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class WebKitGtkPlatform : IHostPlatform
{
    private const string InterfaceSchema = "org.gnome.desktop.interface";

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
        var variable = Environment.GetEnvironmentVariable("GTK_THEME");
        if (!string.IsNullOrEmpty(variable))
        {
            return DesktopTheme.IsDark(variable, colorScheme: null, gtkThemeName: null);
        }

        // GNOME's settings (KDE and others write the GTK theme there too). The schema may be missing,
        // for example on a bare WSL install, so look it up before reading. Reading costs ~10 ms; GTK loads Gio anyway.
        Gio.Module.Initialize();
        using var schema = Gio.SettingsSchemaSource.GetDefault()?.Lookup(InterfaceSchema, recursive: true);
        if (schema is null)
        {
            return false;
        }

        using var settings = Gio.Settings.New(InterfaceSchema);
        return DesktopTheme.IsDark(
            gtkThemeVariable: null,
            colorScheme: schema.HasKey("color-scheme") ? settings.GetString("color-scheme") : null,
            gtkThemeName: schema.HasKey("gtk-theme") ? settings.GetString("gtk-theme") : null);
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