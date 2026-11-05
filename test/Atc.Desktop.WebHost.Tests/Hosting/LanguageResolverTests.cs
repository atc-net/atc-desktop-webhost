namespace Atc.Desktop.WebHost.Tests.Hosting;

public sealed class LanguageResolverTests
{
    private static readonly string[] Supported = ["en", "da-DK", "de-DE"];

    [Theory]
    [InlineData("da-DK", "en-US", "da-DK")]
    [InlineData("da", "en-US", "da-DK")]
    [InlineData(null, "de-AT", "de-DE")]
    [InlineData(null, "da-DK", "da-DK")]
    [InlineData(null, "fr-FR", "en")]
    [InlineData("sv-SE", "de-CH", "de-DE")]
    [InlineData("", "en-GB", "en")]
    public void Resolve_SettingThenOsThenFirst(
        string? setting,
        string osLanguage,
        string expected)
        => LanguageResolver.Resolve(Supported, setting, new System.Globalization.CultureInfo(osLanguage)).Should().Be(expected);

    [Fact]
    public void HostTexts_EveryLanguageHasEveryText()
    {
        var english = HostTexts.KeysOf("en").Order(StringComparer.Ordinal).ToList();

        foreach (var language in HostTexts.Languages)
        {
            HostTexts.KeysOf(language).Order(StringComparer.Ordinal).Should().Equal(english, language);
        }
    }

    [Fact]
    public void HostTexts_UsesTheLanguagePartOfTheTag()
        => HostTexts.Get("da-DK", HostTexts.WindowCouldNotStart).Should().StartWith("Vinduet");
}