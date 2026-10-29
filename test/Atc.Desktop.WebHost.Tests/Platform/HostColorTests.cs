namespace Atc.Desktop.WebHost.Tests.Platform;

public sealed class HostColorTests
{
    [Theory]
    [InlineData("#0d1117", 0x0D, 0x11, 0x17)]
    [InlineData("FFFFFF", 255, 255, 255)]
    public void Parse_ReadsHex(
        string hex,
        byte r,
        byte g,
        byte b)
        => HostColor.Parse(hex).Should().Be(new HostColor(r, g, b));

    [Theory]
    [InlineData("#fff")]
    [InlineData("#gggggg")]
    [InlineData("")]
    public void Parse_RejectsBadInput(string hex)
    {
        var act = () => HostColor.Parse(hex);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ToCss_RoundTrips()
        => HostColor.GitHubDark.ToCss().Should().Be("#0d1117");
}