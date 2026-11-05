namespace Atc.Desktop.WebHost.Bridge;

/// <summary>Payloads of the built-in requests and events. The TypeScript side mirrors these.</summary>
internal static class BuiltInMessages
{
    internal sealed record PathRequest(string Path);

    internal sealed record TitleRequest(string Title);

    internal sealed record UrlRequest(string Url);

    internal sealed record CloseGuardRequest(bool Enabled);

    internal sealed record FindRequest(string? Text);

    internal sealed record PlacementChangedEvent(
        int? Monitor,
        bool Fullscreen,
        bool ShowInTaskbar);

    internal sealed record PlacementRequest(
        int? Monitor,
        bool? Fullscreen,
        bool? ShowInTaskbar);

    internal sealed record FileDialogRequest(
        string? Title,
        string? DefaultPath,
        IReadOnlyList<FileDialogFilter>? Filters);

    internal sealed record FileDialogResult(string? Path);

    internal sealed record DocumentError(
        string Path,
        string Error);

    internal sealed record FileChangedEvent(
        string Path,
        string Kind);

    internal sealed record FolderListRequest(
        string Path,
        string? Pattern,
        bool? Recursive,
        int? MaxFiles);

    internal sealed record FolderEntry(
        string Path,
        string RelativePath,
        long Size,
        DateTime Modified);

    internal sealed record FolderListResult(
        IReadOnlyList<FolderEntry> Files,
        bool Truncated);

    internal sealed record FolderWatchRequest(
        string Path,
        bool? Recursive);

    /// <summary>A file or folder the user chose, remembered across runs (<c>access.list</c>).</summary>
    internal sealed record RememberedPath(
        string Path,
        bool IsFolder,
        DateTime Used);

    internal sealed record FolderChangedEvent(
        string Folder,
        IReadOnlyList<string> Paths,
        bool Rescan);

    /// <summary>Page timings as Unix epoch milliseconds (<c>performance.timeOrigin + performance.now()</c>).</summary>
    internal sealed record FirstPaintRequest(Dictionary<string, double>? Marks);

    internal sealed record HostInfo(
        string AppId,
        string Platform,
        string HostVersion,
        string Runtime,
        IReadOnlyDictionary<string, double> Marks);
}