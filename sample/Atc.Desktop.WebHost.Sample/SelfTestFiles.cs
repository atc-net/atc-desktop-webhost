namespace Atc.Desktop.WebHost.Sample;

/// <summary>Files the self-test reads and watches.</summary>
/// <param name="WatchedFile">A file in a folder the page may use.</param>
/// <param name="OutsideFile">An existing file outside every allowed folder.</param>
/// <param name="TraversalPath">The outside file, reached with <c>..</c> from the allowed folder.</param>
public sealed record SelfTestFiles(
    string WatchedFile,
    string OutsideFile,
    string TraversalPath);