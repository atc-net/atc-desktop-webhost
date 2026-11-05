namespace Atc.Desktop.WebHost.Hosting;

/// <summary>The few texts the host shows itself (before or without the page), in English, Danish and German.</summary>
internal static class HostTexts
{
    public const string WebView2Missing = "webView2Missing";
    public const string WindowCouldNotStart = "windowCouldNotStart";

    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(StringComparer.Ordinal)
        {
            [WebView2Missing] = "The Microsoft Edge WebView2 Runtime is not installed.\n\nInstall it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and start the app again.",
            [WindowCouldNotStart] = "The window could not start:",
        },
        ["da"] = new(StringComparer.Ordinal)
        {
            [WebView2Missing] = "Microsoft Edge WebView2 Runtime er ikke installeret.\n\nInstaller den fra https://go.microsoft.com/fwlink/p/?LinkId=2124703, og start programmet igen.",
            [WindowCouldNotStart] = "Vinduet kunne ikke starte:",
        },
        ["de"] = new(StringComparer.Ordinal)
        {
            [WebView2Missing] = "Die Microsoft Edge WebView2 Runtime ist nicht installiert.\n\nInstallieren Sie sie von https://go.microsoft.com/fwlink/p/?LinkId=2124703 und starten Sie die App erneut.",
            [WindowCouldNotStart] = "Das Fenster konnte nicht gestartet werden:",
        },
    };

    /// <summary>Gets all languages the host has texts for.</summary>
    public static IEnumerable<string> Languages
        => Texts.Keys;

    /// <summary>A text in the given language ("da-DK" uses "da"), English when there is none.</summary>
    /// <param name="language">The resolved UI language.</param>
    /// <param name="key">One of the constants above.</param>
    /// <returns>The text.</returns>
    public static string Get(
        string language,
        string key)
    {
        var dash = language.IndexOf('-', StringComparison.Ordinal);
        var code = dash < 0 ? language : language[..dash];
        return Texts.TryGetValue(code, out var texts) && texts.TryGetValue(key, out var text)
            ? text
            : Texts["en"][key];
    }

    /// <summary>The keys a language has (for the tests).</summary>
    /// <param name="language">A language code.</param>
    /// <returns>The keys.</returns>
    public static IEnumerable<string> KeysOf(string language)
        => Texts[language].Keys;
}