namespace Atc.Desktop.WebHost.Tests.Frontend;

public sealed class FrontendSourceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));

    public FrontendSourceTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "ui", "assets"));
        File.WriteAllText(Path.Combine(root, "ui", "index.html"), "<p>hi</p>");
        File.WriteAllText(Path.Combine(root, "ui", "assets", "app.js"), "1");
        File.WriteAllText(Path.Combine(root, "secret.txt"), "no");
    }

    [Theory]
    [InlineData("https://app.atc-webhost.example/", "index.html")]
    [InlineData("https://app.atc-webhost.example/assets/app.js?v=2", "assets/app.js")]
    [InlineData("https://APP.atc-webhost.example/a%20b.css", "a b.css")]
    [InlineData("https://example.com/index.html", null)]
    public void ToRelativePath_MapsOnlyThePrivateOrigin(
        string address,
        string? expected)
        => FrontendSource.ToRelativePath(new Uri(address)).Should().Be(expected);

    [Theory]
    [InlineData("https://app.atc-webhost.example/index.html", true)]
    [InlineData("http://app.atc-webhost.example/index.html", false)]
    [InlineData("https://app.atc-webhost.example.evil.com/", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    public void IsOnOrigin_AcceptsOnlyHttpsOnTheHostName(
        string? address,
        bool expected)
        => FrontendSource.IsOnOrigin(address).Should().Be(expected);

    [Fact]
    public void UrlFor_BuildsAnAbsoluteUrl()
        => FrontendSource.UrlFor("/index.html").AbsoluteUri.Should().Be("https://app.atc-webhost.example/index.html");

    [Fact]
    public void Folder_ServesFilesBelowTheRoot()
    {
        var source = FrontendSource.FromFolder(Path.Combine(root, "ui"));

        using var stream = source.TryOpen("assets/app.js");

        stream.Should().NotBeNull();
        source.FolderPath.Should().EndWith(Path.DirectorySeparatorChar.ToString());
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("assets/../../secret.txt")]
    [InlineData("missing.js")]
    public void Folder_RefusesPathsOutsideTheRootAndMissingFiles(string path)
    {
        var source = FrontendSource.FromFolder(Path.Combine(root, "ui"));

        using var stream = source.TryOpen(path);

        stream.Should().BeNull();
    }

    [Fact]
    public void Embedded_ServesResourcesByNormalizedPath()
    {
        var source = FrontendSource.FromEmbeddedResources(typeof(FrontendSourceTests).Assembly, "ui/");

        using var page = source.TryOpen("index.html");
        using var script = source.TryOpen("assets/app.js");
        using var missing = source.TryOpen("nope.css");

        page.Should().NotBeNull();
        script.Should().NotBeNull();
        missing.Should().BeNull();
        source.FolderPath.Should().BeNull();
    }

    [Fact]
    public void Embedded_WithoutResources_Throws()
    {
        var act = () => FrontendSource.FromEmbeddedResources(typeof(FrontendSourceTests).Assembly, "nothing/");

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("index.html", "text/html; charset=utf-8")]
    [InlineData("a/b/app.MJS", "text/javascript; charset=utf-8")]
    [InlineData("font.woff2", "font/woff2")]
    [InlineData("data.bin", "application/octet-stream")]
    public void ContentTypes_AreResolvedByExtension(
        string path,
        string expected)
        => ContentTypes.FromPath(path).Should().Be(expected);

    public void Dispose()
        => Directory.Delete(root, recursive: true);
}