namespace Atc.Desktop.WebHost.Files;

/// <summary>Lists the files in a folder for <c>folder.list</c>.</summary>
internal static class FolderLister
{
    /// <summary>The most files one listing returns unless the page asks for fewer.</summary>
    public const int DefaultMaxFiles = 10_000;

    /// <summary>
    /// The files matching the pattern, sorted by relative path. The pattern is a simple file-name pattern such as
    /// <c>*.md</c>, matched without regard to case on every platform. Skipped: names starting with a dot (such as
    /// <c>.git</c>) and what's below them, hidden and system files and folders, and links, so a link can't make a loop.
    /// </summary>
    /// <param name="folder">The full path of the folder.</param>
    /// <param name="pattern">The file-name pattern, or <see langword="null"/> for all files.</param>
    /// <param name="recursive">Include subfolders.</param>
    /// <param name="maxFiles">Stop after this many files.</param>
    /// <param name="cancellationToken">Stops the listing.</param>
    /// <returns>The files, and whether the list was cut at <paramref name="maxFiles"/>.</returns>
    public static BuiltInMessages.FolderListResult List(
        string folder,
        string? pattern,
        bool recursive,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        var namePattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();
        if (namePattern.IndexOfAny(['/', '\\']) >= 0 || namePattern.Contains("..", StringComparison.Ordinal))
        {
            throw new BridgeException("bad_request", "The pattern is a file name pattern such as *.md, without folders.");
        }

        if (!Directory.Exists(folder))
        {
            throw new BridgeException("not_found", "The folder doesn't exist.");
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        };
        var entries = new FileSystemEnumerable<(string Path, long Size, DateTime Modified)>(
            folder,
            (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.Length, entry.LastWriteTimeUtc.UtcDateTime),
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                !entry.IsDirectory &&
                !entry.FileName.StartsWith('.') &&
                FileSystemName.MatchesSimpleExpression(namePattern, entry.FileName, ignoreCase: true),
            ShouldRecursePredicate = (ref FileSystemEntry entry) => !entry.FileName.StartsWith('.'),
        };

        var files = new List<BuiltInMessages.FolderEntry>();
        var truncated = false;
        foreach (var (path, size, modified) in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (files.Count >= maxFiles)
            {
                truncated = true;
                break;
            }

            var relativePath = Path.GetRelativePath(folder, path).Replace('\\', '/');
            files.Add(new BuiltInMessages.FolderEntry(path, relativePath, size, modified));
        }

        files.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.RelativePath, b.RelativePath));
        return new BuiltInMessages.FolderListResult(files, truncated);
    }
}