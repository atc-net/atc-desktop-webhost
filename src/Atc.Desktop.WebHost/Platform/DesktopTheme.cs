namespace Atc.Desktop.WebHost.Platform;

/// <summary>Decides whether a Linux desktop is in dark mode, from the settings GTK apps follow.</summary>
internal static class DesktopTheme
{
    /// <summary>Checks the settings in order: the <c>GTK_THEME</c> variable, the colour scheme, the GTK theme name.</summary>
    /// <param name="gtkThemeVariable">The <c>GTK_THEME</c> environment variable, e.g. <c>Adwaita:dark</c>.</param>
    /// <param name="colorScheme">GNOME's <c>org.gnome.desktop.interface color-scheme</c>: <c>default</c>, <c>prefer-dark</c> or <c>prefer-light</c>.</param>
    /// <param name="gtkThemeName">GNOME's <c>org.gnome.desktop.interface gtk-theme</c>, e.g. <c>Yaru-dark</c> or <c>Breeze-Dark</c>.</param>
    /// <returns>True for dark.</returns>
    public static bool IsDark(
        string? gtkThemeVariable,
        string? colorScheme,
        string? gtkThemeName)
    {
        if (!string.IsNullOrEmpty(gtkThemeVariable))
        {
            // An explicit GTK_THEME wins, light or dark.
            return gtkThemeVariable.EndsWith(":dark", StringComparison.OrdinalIgnoreCase) ||
                   IsDarkThemeName(gtkThemeVariable);
        }

        return colorScheme switch
        {
            "prefer-dark" => true,
            "prefer-light" => false,
            _ => IsDarkThemeName(gtkThemeName),
        };
    }

    private static bool IsDarkThemeName(string? name)
        => name is not null &&
           (name.EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("_dark", StringComparison.OrdinalIgnoreCase));
}