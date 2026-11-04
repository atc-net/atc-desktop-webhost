namespace Atc.Desktop.WebHost.Tests.Hosting;

public sealed class HostScriptTests
{
    [Fact]
    public void Build_GivesTheBootDataToTheTopFrameOnly()
    {
        var script = HostScript.Build("{\"secret\":1}");

        script.Should().StartWith("if (window === window.top) {");
        script.IndexOf("{\"secret\":1}", StringComparison.Ordinal).Should().BeGreaterThan(script.IndexOf("window.top", StringComparison.Ordinal));
        script.TrimEnd().Should().EndWith("}");
    }
}