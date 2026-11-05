namespace Atc.Desktop.WebHost.Hosting;

/// <summary>
/// The OS UI language. .NET's UI culture is the invariant culture when the app sets <c>InvariantGlobalization</c>, which
/// hides the OS language; then it's read from the OS: on Windows the user's preferred UI language, elsewhere the
/// <c>LC_ALL</c>, <c>LC_MESSAGES</c> and <c>LANG</c> variables.
/// </summary>
internal static partial class OsLanguage
{
    private const uint MuiLanguageName = 0x8;

    /// <summary>Gets the OS UI language as a tag like <c>da-DK</c>, or <see langword="null"/> when unknown.</summary>
    /// <returns>The language tag, or <see langword="null"/>.</returns>
    public static string? Get()
        => FromCulture(CultureInfo.CurrentUICulture)
           ?? (OperatingSystem.IsWindows() ? FromWindows() : FromEnvironment(Environment.GetEnvironmentVariable));

    /// <summary>The culture's name, or <see langword="null"/> for the invariant culture.</summary>
    /// <param name="culture">The UI culture.</param>
    /// <returns>The language tag, or <see langword="null"/>.</returns>
    public static string? FromCulture(CultureInfo culture)
        => string.IsNullOrEmpty(culture.Name) ? null : culture.Name;

    /// <summary>
    /// The language from the POSIX locale variables, in their order of precedence. <c>de_DE.UTF-8@euro</c> gives
    /// <c>de-DE</c>; <c>C</c> and <c>POSIX</c> give <see langword="null"/>.
    /// </summary>
    /// <param name="getVariable">Reads an environment variable.</param>
    /// <returns>The language tag, or <see langword="null"/>.</returns>
    public static string? FromEnvironment(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);

        foreach (var name in (string[])["LC_ALL", "LC_MESSAGES", "LANG"])
        {
            var value = getVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            // The first variable that is set decides, even when it says C.
            var end = value.IndexOfAny(['.', '@']);
            var locale = (end < 0 ? value : value[..end]).Trim();
            return locale.Length == 0 || locale is "C" or "POSIX" ? null : locale.Replace('_', '-');
        }

        return null;
    }

    /// <summary>The first of the Windows user's preferred UI languages, or <see langword="null"/>.</summary>
    /// <returns>The language tag, or <see langword="null"/>.</returns>
    [SupportedOSPlatform("windows")]
    public static string? FromWindows()
    {
        uint size = 0;
        if (!GetUserPreferredUILanguages(MuiLanguageName, out _, null, ref size) || size == 0)
        {
            return null;
        }

        // A list of names, each ending in a null character, then one more null character.
        var buffer = new char[size];
        if (!GetUserPreferredUILanguages(MuiLanguageName, out _, buffer, ref size))
        {
            return null;
        }

        var end = Array.IndexOf(buffer, '\0');
        var first = new string(buffer, 0, end < 0 ? buffer.Length : end);
        return first.Length == 0 ? null : first;
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetUserPreferredUILanguages(
        uint flags,
        out uint languageCount,
        [Out] char[]? buffer,
        ref uint bufferSize);
}