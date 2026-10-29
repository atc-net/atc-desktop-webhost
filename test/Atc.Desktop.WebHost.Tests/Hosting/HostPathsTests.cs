namespace Atc.Desktop.WebHost.Tests.Hosting;

public sealed class HostPathsTests : IDisposable
{
    private readonly string exeFolder = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));

    public HostPathsTests()
        => Directory.CreateDirectory(exeFolder);

    [Fact]
    public void Resolve_UsesTheUserFolders()
    {
        var paths = HostPaths.Resolve("Vendor", "App", exeFolder);

        paths.IsPortable.Should().BeFalse();
        paths.ConfigFolder.Should().EndWith(Path.Combine("Vendor", "App"));
        paths.DataFolder.Should().EndWith(Path.Combine("Vendor", "App"));
        paths.WebViewFolder.Should().StartWith(paths.DataFolder);
    }

    [Fact]
    public void Resolve_WithPortableFlag_UsesTheExeFolder()
    {
        File.WriteAllText(Path.Combine(exeFolder, HostPaths.PortableFlagFileName), string.Empty);

        var paths = HostPaths.Resolve("Vendor", "App", exeFolder);

        paths.IsPortable.Should().BeTrue();
        paths.ConfigFolder.Should().Be(Path.Combine(exeFolder, "data"));
    }

    [Fact]
    public void SingleInstanceName_IsSanitized()
        => SingleInstance.BuildName("My App/1", @"DOMAIN\user").Should().Be("AtcWebHost.My_App_1.DOMAIN_user");

    public void Dispose()
        => Directory.Delete(exeFolder, recursive: true);
}