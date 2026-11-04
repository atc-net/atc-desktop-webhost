namespace Atc.Desktop.WebHost.Frontend;

/// <summary>
/// Extra files the page can load from a second private origin, next to the app's own frontend: for example images
/// from the document's folder, or the pages of plug-in modules. A mount named "doc" is served at
/// <c>https://files.atc-webhost.example/doc/...</c> on Windows and <c>atc-app://files/doc/...</c> on Linux
/// (the page finds the origin in <c>atcHost.boot.filesOrigin</c>).
/// <para>
/// The app's frontend keeps the fast native file serving; only mounted files go through <see cref="FrontendSource.TryOpen"/>.
/// Mounted files are read into memory, so nothing stays open or locked, and can be replaced while they are shown.
/// </para>
/// </summary>
/// <param name="Name">The first path segment, e.g. "doc". Letters, digits, '-', '_' and '.'.</param>
/// <param name="Source">Where the files come from. It decides what may be served (and must refuse paths outside its root).</param>
public sealed record FrontendMount(
    string Name,
    FrontendSource Source)
{
    /// <summary>
    /// Gets the Cache-Control header sent with every file, or <see langword="null"/> for none. The default,
    /// "no-cache", makes the webview check again each time, so a changed file shows up on the next load.
    /// </summary>
    public string? CacheControl { get; init; } = "no-cache";
}