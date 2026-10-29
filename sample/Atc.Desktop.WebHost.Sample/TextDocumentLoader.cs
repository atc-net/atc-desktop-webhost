namespace Atc.Desktop.WebHost.Sample;

/// <summary>Loads a text file into the payload the page shows. Runs on the thread pool, in parallel with the window start.</summary>
public static class TextDocumentLoader
{
    /// <summary>Reads the file.</summary>
    /// <param name="path">The full path.</param>
    /// <param name="cancellationToken">Cancelled when the app closes.</param>
    /// <returns>The document payload.</returns>
    public static async ValueTask<JsonPayload> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var lines = text.AsSpan().Count('\n') + (text.Length > 0 && text[^1] != '\n' ? 1 : 0);
        var document = new TextDocument(
            path,
            Path.GetFileName(path),
            text,
            lines,
            new FileInfo(path).Length,
            Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2));
        return JsonPayload.From(document, SampleJsonContext.Default.TextDocument);
    }
}