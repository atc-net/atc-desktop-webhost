namespace Atc.Desktop.WebHost.Hosting;

/// <summary>
/// Which addresses an iframe may load: web pages (http, https), the app's own origins, and inline content
/// (<c>about:</c>, <c>data:</c>, <c>blob:</c>). Everything else (<c>file:</c>, <c>ms-settings:</c>, <c>mailto:</c>,
/// other app schemes) is refused, so a frame can't read local files or start other programs.
/// </summary>
internal static class FramePolicy
{
    private static readonly string[] InlineSchemes = ["about:", "data:", "blob:"];

    /// <summary>Checks an iframe navigation.</summary>
    /// <param name="address">The address the frame navigates to.</param>
    /// <param name="appOrigin">The app's origin, e.g. "atc-app://app".</param>
    /// <param name="filesOrigin">The mounts' origin, e.g. "atc-app://files".</param>
    /// <returns><see langword="true"/> when the frame may load it.</returns>
    public static bool MayLoadInFrame(
        string? address,
        string appOrigin,
        string filesOrigin)
    {
        if (string.IsNullOrEmpty(address))
        {
            return false;
        }

        if (InlineSchemes.Any(s => address.StartsWith(s, StringComparison.OrdinalIgnoreCase)) ||
            IsUnder(address, appOrigin) ||
            IsUnder(address, filesOrigin))
        {
            return true;
        }

        return Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static bool IsUnder(
        string address,
        string origin)
        => address.Length > origin.Length &&
           address.StartsWith(origin, StringComparison.OrdinalIgnoreCase) &&
           address[origin.Length] == '/';
}