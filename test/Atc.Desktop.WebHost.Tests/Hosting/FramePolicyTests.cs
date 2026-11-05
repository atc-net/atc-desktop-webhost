namespace Atc.Desktop.WebHost.Tests.Hosting;

public sealed class FramePolicyTests
{
    [Theory]
    [InlineData("https://www.youtube.com/embed/x")]
    [InlineData("http://example.com/")]
    [InlineData("about:blank")]
    [InlineData("about:srcdoc")]
    [InlineData("data:text/html,hi")]
    [InlineData("blob:https://app.atc-webhost.example/1234")]
    [InlineData("atc-app://app/index.html")]
    [InlineData("atc-app://files/modules/clock/index.html")]
    public void WebAndInlineAddresses_MayLoad(string address)
        => FramePolicy.MayLoadInFrame(address, "atc-app://app", "atc-app://files").Should().BeTrue();

    [Theory]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ms-settings:privacy")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("atc-app://other/x")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherAddresses_AreRefused(string? address)
        => FramePolicy.MayLoadInFrame(address, "atc-app://app", "atc-app://files").Should().BeFalse();
}