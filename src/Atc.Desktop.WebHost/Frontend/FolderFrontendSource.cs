namespace Atc.Desktop.WebHost.Frontend;

/// <summary>Frontend files in a folder on disk.</summary>
internal sealed class FolderFrontendSource : FrontendSource
{
    private readonly string root;

    public FolderFrontendSource(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
    }

    public override string FolderPath
        => root;

    public override Stream? TryOpen(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            !File.Exists(fullPath))
        {
            return null;
        }

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1, FileOptions.SequentialScan);
    }
}