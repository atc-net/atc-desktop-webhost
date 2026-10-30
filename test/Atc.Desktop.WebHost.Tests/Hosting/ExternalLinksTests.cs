namespace Atc.Desktop.WebHost.Tests.Hosting;

public sealed class ExternalLinksTests
{
    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("http://example.com")]
    [InlineData("mailto:someone@example.com")]
    public void WebAndMailLinks_AreAllowed(string link)
        => ExternalLinks.TryParse(link, out _).Should().BeTrue();

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:privacy")]
    [InlineData("relative/page.html")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherLinks_AreRefused(string? link)
        => ExternalLinks.TryParse(link, out _).Should().BeFalse();
}