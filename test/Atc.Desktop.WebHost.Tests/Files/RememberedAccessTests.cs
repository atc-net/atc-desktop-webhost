namespace Atc.Desktop.WebHost.Tests.Files;

public sealed class RememberedAccessTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Recorded_IsReadBackByTheNextRun_MostRecentFirst()
    {
        var first = new RememberedAccess(folder);
        first.Record(Path.Combine(folder, "docs", "a.md"), isFolder: false);
        first.Record(Path.Combine(folder, "wiki"), isFolder: true);
        first.Flush(TimeSpan.FromSeconds(5));

        var next = new RememberedAccess(folder).GetAll();

        next.Select(e => (e.Path, e.IsFolder)).Should().Equal(
            (Path.Combine(folder, "wiki"), true),
            (Path.Combine(folder, "docs", "a.md"), false));
    }

    [Fact]
    public void RecordedAgain_MovesToTheTop_WithoutADuplicate()
    {
        var access = new RememberedAccess(folder);
        access.Record(Path.Combine(folder, "a.md"), isFolder: false);
        access.Record(Path.Combine(folder, "b.md"), isFolder: false);
        access.Record(Path.Combine(folder, "a.md"), isFolder: false);
        access.Flush(TimeSpan.FromSeconds(5));

        access.GetAll().Select(e => Path.GetFileName(e.Path)).Should().Equal("a.md", "b.md");
    }

    [Fact]
    public void List_KeepsTheMostRecent()
    {
        var access = new RememberedAccess(folder);
        for (var i = 0; i < RememberedAccess.MaxEntries + 5; i++)
        {
            access.Record(Path.Combine(folder, $"{i}.md"), isFolder: false);
        }

        access.Flush(TimeSpan.FromSeconds(5));

        var all = access.GetAll();
        all.Should().HaveCount(RememberedAccess.MaxEntries);
        Path.GetFileName(all[0].Path).Should().Be($"{RememberedAccess.MaxEntries + 4}.md");
    }

    [Fact]
    public void Forgotten_IsGoneInTheNextRun()
    {
        var access = new RememberedAccess(folder);
        access.Record(Path.Combine(folder, "a.md"), isFolder: false);
        access.Forget(Path.Combine(folder, "a.md")).Should().BeTrue();
        access.Flush(TimeSpan.FromSeconds(5));

        new RememberedAccess(folder).GetAll().Should().BeEmpty();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"path\":\"x\"}")]
    [InlineData("[{\"path\":\"relative/a.md\",\"isFolder\":false,\"used\":\"2026-11-05T00:00:00Z\"}]")]
    public void BrokenOrRelativeEntries_AreIgnored(string content)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "file-access.json"), content);

        new RememberedAccess(folder).GetAll().Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}