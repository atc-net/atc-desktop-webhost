namespace Atc.Desktop.WebHost.Tests.Frontend;

public sealed class MountTableTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));
    private readonly MountTable table;

    public MountTableTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "doc", "images"));
        File.WriteAllText(Path.Combine(root, "doc", "images", "logo.svg"), "<svg/>");
        File.WriteAllText(Path.Combine(root, "secret.txt"), "no");
        table = new MountTable([new FrontendMount("doc", FrontendSource.FromFolder(Path.Combine(root, "doc")))]);
    }

    [Fact]
    public void Open_FileInAMount_IsReadIntoMemory()
    {
        var file = table.Open("doc/images/logo.svg");

        file.Should().NotBeNull();
        using var reader = new StreamReader(file!.Content);
        reader.ReadToEnd().Should().Be("<svg/>");
        file.ContentType.Should().Be("image/svg+xml");
        file.CacheControl.Should().Be("no-cache");
    }

    [Fact]
    public void Open_MountNameIgnoresCase()
        => table.Open("DOC/images/logo.svg").Should().NotBeNull();

    [Theory]
    [InlineData("doc/../secret.txt")]
    [InlineData("doc/images/../../secret.txt")]
    [InlineData(@"doc\..\secret.txt")]
    [InlineData("other/images/logo.svg")]
    [InlineData("doc/images/missing.png")]
    [InlineData("doc")]
    [InlineData("")]
    public void Open_OutsideOrUnknown_IsNull(string path)
        => table.Open(path).Should().BeNull();

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("doc folder")]
    public void Constructor_BadName_Throws(string name)
    {
        var create = () => new MountTable([new FrontendMount(name, FrontendSource.FromFolder(root))]);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_SameNameTwice_Throws()
    {
        var create = () => new MountTable(
        [
            new FrontendMount("doc", FrontendSource.FromFolder(root)),
            new FrontendMount("DOC", FrontendSource.FromFolder(root)),
        ]);

        create.Should().Throw<ArgumentException>();
    }

    public void Dispose()
        => Directory.Delete(root, recursive: true);
}