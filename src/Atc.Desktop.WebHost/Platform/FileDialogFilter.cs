namespace Atc.Desktop.WebHost.Platform;

/// <summary>A file type filter, e.g. ("Markdown", ["*.md", "*.markdown"]).</summary>
/// <param name="Name">The label shown in the dialog.</param>
/// <param name="Patterns">Glob patterns such as "*.md".</param>
public sealed record FileDialogFilter(
    string Name,
    IReadOnlyList<string> Patterns);