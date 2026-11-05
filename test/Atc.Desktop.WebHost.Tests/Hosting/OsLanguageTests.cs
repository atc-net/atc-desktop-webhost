namespace Atc.Desktop.WebHost.Tests.Hosting;

public sealed class OsLanguageTests
{
    [Theory]
    [InlineData(null, null, "de_DE.UTF-8", "de-DE")]
    [InlineData(null, "da_DK.UTF-8", "en_US.UTF-8", "da-DK")]
    [InlineData("sv_SE", "da_DK", "en_US", "sv-SE")]
    [InlineData(null, null, "de_DE@euro", "de-DE")]
    [InlineData(null, null, "fr", "fr")]
    [InlineData("", " ", "en_GB.UTF-8", "en-GB")]
    [InlineData(null, null, "C.UTF-8", null)]
    [InlineData("POSIX", null, "da_DK.UTF-8", null)]
    [InlineData(null, null, null, null)]
    public void FromEnvironment_FirstSetVariableDecides(
        string? lcAll,
        string? lcMessages,
        string? lang,
        string? expected)
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["LC_ALL"] = lcAll,
            ["LC_MESSAGES"] = lcMessages,
            ["LANG"] = lang,
        };

        OsLanguage.FromEnvironment(name => variables.GetValueOrDefault(name)).Should().Be(expected);
    }

    [Fact]
    public void FromCulture_InvariantGivesNull()
        => OsLanguage.FromCulture(System.Globalization.CultureInfo.InvariantCulture).Should().BeNull();

    [Fact]
    public void FromWindows_GivesTheUsersUiLanguage()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OsLanguage.FromWindows().Should().Be(System.Globalization.CultureInfo.CurrentUICulture.Name);
    }
}