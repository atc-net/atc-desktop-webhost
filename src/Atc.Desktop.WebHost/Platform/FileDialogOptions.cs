namespace Atc.Desktop.WebHost.Platform;

/// <summary>Options for the native Open and Save dialogs.</summary>
public sealed class FileDialogOptions
{
    /// <summary>Gets the dialog title, or <see langword="null"/> for the OS default.</summary>
    public string? Title { get; init; }

    /// <summary>Gets a start folder (Open) or suggested file path (Save), or <see langword="null"/>.</summary>
    public string? DefaultPath { get; init; }

    /// <summary>Gets the file type filters.</summary>
    public IReadOnlyList<FileDialogFilter> Filters { get; init; } = [];
}