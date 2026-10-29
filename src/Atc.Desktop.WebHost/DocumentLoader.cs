namespace Atc.Desktop.WebHost;

/// <summary>Turns a file path into the JSON the page shows. Runs on a thread-pool thread.</summary>
/// <param name="path">The full path.</param>
/// <param name="cancellationToken">Cancelled when the app closes.</param>
/// <returns>The document payload.</returns>
public delegate ValueTask<JsonPayload> DocumentLoader(
    string path,
    CancellationToken cancellationToken);