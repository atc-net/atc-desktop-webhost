namespace Atc.Desktop.WebHost.Hosting;

/// <summary>Picks the UI language from the app's supported languages.</summary>
internal static class LanguageResolver
{
    /// <summary>
    /// The user's <c>language</c> setting when the app supports it, else the OS UI language, else the first supported
    /// language. Matches the full tag first ("da-DK"), then the language alone ("da" matches "da-DK" and the other way round).
    /// </summary>
    /// <param name="supported">The app's languages; the first is the fallback.</param>
    /// <param name="setting">The <c>language</c> setting, or <see langword="null"/>.</param>
    /// <param name="osLanguage">The OS UI language, e.g. <c>da-DK</c>, or <see langword="null"/>.</param>
    /// <returns>One of <paramref name="supported"/>.</returns>
    public static string Resolve(
        IReadOnlyList<string> supported,
        string? setting,
        string? osLanguage)
    {
        if (supported.Count == 0)
        {
            return "en";
        }

        return Match(supported, setting) ?? Match(supported, osLanguage) ?? supported[0];
    }

    private static string? Match(
        IReadOnlyList<string> supported,
        string? wanted)
    {
        if (string.IsNullOrWhiteSpace(wanted))
        {
            return null;
        }

        var exact = supported.FirstOrDefault(s => string.Equals(s, wanted, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var language = LanguageOf(wanted);
        return supported.FirstOrDefault(s => string.Equals(LanguageOf(s), language, StringComparison.OrdinalIgnoreCase));
    }

    private static string LanguageOf(string tag)
    {
        var dash = tag.IndexOfAny(['-', '_']);
        return dash < 0 ? tag : tag[..dash];
    }
}