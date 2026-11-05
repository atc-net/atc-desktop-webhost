namespace Atc.Desktop.WebHost.Tests.Files;

public sealed class FolderListerTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));

    public FolderListerTests()
    {
        foreach (var file in (string[])["a.md", "B.MD", "notes.txt", "sub/c.md", "sub/deep/d.md", ".git/x.md", ".hidden.md", "sub/.obsidian/y.md"])
        {
            var path = Path.Combine(folder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file);
        }
    }

    [Fact]
    public void Recursive_ListsMatchingFilesInSubfolders_AnyCase_SkipsDotNames()
        => List("*.md", recursive: true).Files.Select(f => f.RelativePath).Should()
            .Equal("a.md", "B.MD", "sub/c.md", "sub/deep/d.md");

    [Fact]
    public void NotRecursive_ListsOnlyTheFolder()
        => List("*.md", recursive: false).Files.Select(f => f.RelativePath).Should().Equal("a.md", "B.MD");

    [Fact]
    public void NoPattern_ListsEveryFile()
        => List(null, recursive: false).Files.Select(f => f.RelativePath).Should().Equal("a.md", "B.MD", "notes.txt");

    [Fact]
    public void Entries_HaveFullPathSizeAndTime()
    {
        var entry = List("a.md", recursive: false).Files.Should().ContainSingle().Subject;

        entry.Path.Should().Be(Path.Combine(folder, "a.md"));
        entry.Size.Should().Be(4);
        entry.Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void MaxFiles_CutsTheListAndSaysSo()
    {
        var result = FolderLister.List(folder, "*.md", recursive: true, maxFiles: 2, TestContext.Current.CancellationToken);

        result.Files.Should().HaveCount(2);
        result.Truncated.Should().BeTrue();
    }

    [Theory]
    [InlineData("../*.md")]
    [InlineData("sub/*.md")]
    [InlineData("..")]
    public void PatternWithFolders_IsABadRequest(string pattern)
        => FluentActions.Invoking(() => List(pattern, recursive: true))
            .Should().Throw<BridgeException>().Which.Code.Should().Be("bad_request");

    [Fact]
    public void MissingFolder_IsNotFound()
        => FluentActions.Invoking(() => FolderLister.List(Path.Combine(folder, "nope"), "*.md", recursive: true, 10, TestContext.Current.CancellationToken))
            .Should().Throw<BridgeException>().Which.Code.Should().Be("not_found");

    public void Dispose()
        => Directory.Delete(folder, recursive: true);

    private BuiltInMessages.FolderListResult List(
        string? pattern,
        bool recursive)
        => FolderLister.List(folder, pattern, recursive, FolderLister.DefaultMaxFiles, TestContext.Current.CancellationToken);
}