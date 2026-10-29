namespace Atc.Desktop.WebHost.Sample;

/// <summary>The document the sample page shows.</summary>
/// <param name="Path">The full path.</param>
/// <param name="Name">The file name.</param>
/// <param name="Text">The file content.</param>
/// <param name="Lines">The number of lines.</param>
/// <param name="Bytes">The file size.</param>
/// <param name="LoadMs">How long reading took.</param>
public sealed record TextDocument(
    string Path,
    string Name,
    string Text,
    int Lines,
    long Bytes,
    double LoadMs);