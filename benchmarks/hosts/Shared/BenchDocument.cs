namespace AtcBench;

/// <summary>The document the page shows. Same shape as the sample's TextDocument.</summary>
public sealed record BenchDocument(
    string Path,
    string Name,
    string Text,
    int Lines,
    long Bytes,
    double LoadMs);
