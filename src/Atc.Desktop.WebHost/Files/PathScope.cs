namespace Atc.Desktop.WebHost.Files;

/// <summary>
/// The files the page may ask the host to read or watch. A file the user chose (command line, a second launch,
/// an Open or Save dialog) grants its folder and everything below it. Everything else is refused, so a page
/// (or script injected into it) can't read arbitrary files. Thread-safe.
/// </summary>
internal sealed class PathScope
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private readonly Lock gate = new();
    private readonly List<string> grantedFolders = [];
    private readonly bool isRestricted;

    /// <summary>Initializes a new instance of the <see cref="PathScope"/> class.</summary>
    /// <param name="isRestricted">False allows every path (the app opted out).</param>
    public PathScope(bool isRestricted)
        => this.isRestricted = isRestricted;

    /// <summary>Allows the file's folder and everything below it.</summary>
    /// <param name="filePath">A file the user chose.</param>
    public void Grant(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var folder = WithTrailingSeparator(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? Path.GetFullPath(filePath));
        lock (gate)
        {
            if (!grantedFolders.Exists(f => folder.StartsWith(f, PathComparison)))
            {
                grantedFolders.RemoveAll(f => f.StartsWith(folder, PathComparison));
                grantedFolders.Add(folder);
            }
        }
    }

    /// <summary>Checks a path.</summary>
    /// <param name="path">Any path; relative segments are resolved first.</param>
    /// <returns>True when the page may use it.</returns>
    public bool IsAllowed(string path)
    {
        if (!isRestricted)
        {
            return true;
        }

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        lock (gate)
        {
            return grantedFolders.Exists(f => fullPath.StartsWith(f, PathComparison));
        }
    }

    /// <summary>Checks a path for a bridge handler.</summary>
    /// <param name="path">The path from the page.</param>
    /// <returns>The full path.</returns>
    /// <exception cref="BridgeException">With code <c>forbidden</c> when the path is outside the scope.</exception>
    public string Require(string path)
        => IsAllowed(path)
            ? Path.GetFullPath(path)
            : throw new BridgeException("forbidden", "The file is outside the folders the user opened.");

    private static string WithTrailingSeparator(string folder)
        => Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar;
}