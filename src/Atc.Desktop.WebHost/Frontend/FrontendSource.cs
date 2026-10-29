namespace Atc.Desktop.WebHost.Frontend;

/// <summary>
/// Where the frontend files (index.html, scripts, styles) come from. The page is served from a
/// private origin (<see cref="Origin"/>), never from <c>file://</c>, so <c>fetch</c> and modules work.
/// </summary>
public abstract class FrontendSource
{
    /// <summary>The host name of the private origin the frontend is served from.</summary>
    public const string HostName = "app.atc-webhost.example";

    /// <summary>Gets the private origin, e.g. "https://app.atc-webhost.example".</summary>
    public static string Origin => "https://" + HostName;

    /// <summary>
    /// Gets the folder on disk, or <see langword="null"/>. When set, WebView2 serves it natively with
    /// virtual host mapping (no managed code per request).
    /// </summary>
    public virtual string? FolderPath => null;

    /// <summary>Serves files from a folder on disk, e.g. the published "wwwroot".</summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The source.</returns>
    public static FrontendSource FromFolder(string folder)
        => new FolderFrontendSource(folder);

    /// <summary>
    /// Serves files embedded in an assembly. Embed them with a LogicalName that starts with
    /// <paramref name="prefix"/>, e.g. <c>LogicalName="wwwroot/%(RecursiveDir)%(Filename)%(Extension)"</c>.
    /// </summary>
    /// <param name="assembly">The assembly holding the resources.</param>
    /// <param name="prefix">The LogicalName prefix, e.g. "wwwroot/".</param>
    /// <returns>The source.</returns>
    public static FrontendSource FromEmbeddedResources(
        Assembly assembly,
        string prefix)
        => new EmbeddedFrontendSource(assembly, prefix);

    /// <summary>Opens a file.</summary>
    /// <param name="relativePath">The path below the root, with '/' separators, e.g. "assets/app.js".</param>
    /// <returns>The stream, or <see langword="null"/> when the file doesn't exist or is outside the root.</returns>
    public abstract Stream? TryOpen(string relativePath);

    /// <summary>Builds the URL of a frontend file.</summary>
    /// <param name="relativePath">The path below the root, e.g. "index.html".</param>
    /// <returns>The absolute URL on the private origin.</returns>
    public static Uri UrlFor(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return new Uri(new Uri(Origin), relativePath.TrimStart('/'));
    }

    /// <summary>Checks whether a URL is on the private origin.</summary>
    /// <param name="address">The URL text.</param>
    /// <returns><see langword="true"/> when the URL belongs to the app's page.</returns>
    public static bool IsOnOrigin(string? address)
        => Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
           uri.Scheme == Uri.UriSchemeHttps &&
           string.Equals(uri.Host, HostName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Turns a request URL into a path below the root.</summary>
    /// <param name="uri">The URL, e.g. "https://app.atc-webhost.example/assets/app.js?v=1".</param>
    /// <returns>The relative path, or <see langword="null"/> when the URL isn't on the private origin.</returns>
    public static string? ToRelativePath(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri ||
            !string.Equals(uri.Host, HostName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
        return path.Length == 0 ? "index.html" : path;
    }
}