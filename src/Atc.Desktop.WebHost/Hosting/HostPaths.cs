namespace Atc.Desktop.WebHost.Hosting;

/// <summary>
/// Where an app keeps its files.
/// <list type="bullet">
/// <item>Windows: settings in <c>%APPDATA%\{Vendor}\{AppId}</c>, data (webview profile, logs) in <c>%LOCALAPPDATA%\{Vendor}\{AppId}</c>.</item>
/// <item>Linux: <c>$XDG_CONFIG_HOME/{Vendor}/{AppId}</c> and <c>$XDG_DATA_HOME/{Vendor}/{AppId}</c> (.NET maps the special folders to XDG).</item>
/// <item>Portable: when a file named <c>portable.flag</c> sits next to the executable, both live in <c>data</c> next to it.</item>
/// </list>
/// </summary>
public sealed class HostPaths
{
    /// <summary>The marker file that switches on portable mode.</summary>
    public const string PortableFlagFileName = "portable.flag";

    private HostPaths(
        string configFolder,
        string dataFolder,
        bool isPortable)
    {
        ConfigFolder = configFolder;
        DataFolder = dataFolder;
        IsPortable = isPortable;
    }

    /// <summary>Gets the folder for settings (roams on Windows).</summary>
    public string ConfigFolder { get; }

    /// <summary>Gets the folder for machine-local data: the webview profile, logs.</summary>
    public string DataFolder { get; }

    /// <summary>Gets a value indicating whether the app runs in portable mode.</summary>
    public bool IsPortable { get; }

    /// <summary>Gets the webview profile folder.</summary>
    public string WebViewFolder
        => Path.Combine(DataFolder, "WebView");

    /// <summary>Resolves the folders for an app. Doesn't create them.</summary>
    /// <param name="vendor">The vendor folder, e.g. "AtcSoft".</param>
    /// <param name="appId">The app id.</param>
    /// <param name="baseDirectory">The executable folder (portable check); defaults to <see cref="AppContext.BaseDirectory"/>.</param>
    /// <returns>The paths.</returns>
    public static HostPaths Resolve(
        string vendor,
        string appId,
        string? baseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendor);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        var exeFolder = baseDirectory ?? AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(exeFolder, PortableFlagFileName)))
        {
            var portable = Path.Combine(exeFolder, "data");
            return new HostPaths(portable, portable, isPortable: true);
        }

        return new HostPaths(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), vendor, appId),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), vendor, appId),
            isPortable: false);
    }
}