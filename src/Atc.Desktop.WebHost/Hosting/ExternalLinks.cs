namespace Atc.Desktop.WebHost.Hosting;

/// <summary>Opens links in the user's default app. Only http, https and mailto: never files or other schemes.</summary>
internal static class ExternalLinks
{
    /// <summary>Checks whether a link may be opened outside the app.</summary>
    /// <param name="url">The link.</param>
    /// <param name="uri">The parsed link, when allowed.</param>
    /// <returns>True for absolute http, https and mailto links.</returns>
    public static bool TryParse(
        string? url,
        [NotNullWhen(true)] out Uri? uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
            parsed.Scheme is "http" or "https" or "mailto")
        {
            uri = parsed;
            return true;
        }

        uri = null;
        return false;
    }

    /// <summary>Opens the link in the default browser or mail app, if it's allowed.</summary>
    /// <param name="url">The link.</param>
    /// <returns>True when it was opened.</returns>
    public static bool TryOpen(string? url)
    {
        if (!TryParse(url, out var uri))
        {
            return false;
        }

        using var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        return true;
    }
}