namespace Atc.Desktop.WebHost.Bridge;

/// <summary>Payloads of the built-in requests and events. The TypeScript side mirrors these.</summary>
internal static class BuiltInMessages
{
    internal sealed record PathRequest(string Path);

    internal sealed record TitleRequest(string Title);

    internal sealed record UrlRequest(string Url);

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

    /// <summary>Page timings as Unix epoch milliseconds (<c>performance.timeOrigin + performance.now()</c>).</summary>
    internal sealed record FirstPaintRequest(Dictionary<string, double>? Marks);

    internal sealed record HostInfo(
        string AppId,
        string Platform,
        string HostVersion,
        string Runtime,
        IReadOnlyDictionary<string, double> Marks);
}