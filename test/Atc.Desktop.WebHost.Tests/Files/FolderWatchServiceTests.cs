namespace Atc.Desktop.WebHost.Tests.Files;

public sealed class FolderWatchServiceTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));
    private readonly List<(string Folder, IReadOnlyList<string> Paths, bool Rescan)> reports = [];

    public FolderWatchServiceTests()
        => Directory.CreateDirectory(Path.Combine(folder, "sub"));

    [Fact]
    public async Task NewFileInASubfolder_IsReportedOnce()
    {
        using var service = CreateService();
        service.Watch(folder, recursive: true);

        var path = Path.Combine(folder, "sub", "new.md");
        await File.WriteAllTextAsync(path, "1", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, "2", TestContext.Current.CancellationToken);

        await WaitUntilAsync(() => Count() > 0);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        var report = Snapshot().Should().ContainSingle().Subject;
        report.Folder.Should().Be(folder);
        report.Paths.Should().Contain(path);
        report.Rescan.Should().BeFalse();
    }

    [Fact]
    public async Task RenamedFile_ReportsBothPaths()
    {
        var oldPath = Path.Combine(folder, "old.md");
        await File.WriteAllTextAsync(oldPath, "x", TestContext.Current.CancellationToken);
        using var service = CreateService();
        service.Watch(folder, recursive: false);

        var newPath = Path.Combine(folder, "new.md");
        File.Move(oldPath, newPath);

        await WaitUntilAsync(() => Count() > 0);

        Snapshot().SelectMany(r => r.Paths).Should().Contain([oldPath, newPath]);
    }

    [Fact]
    public async Task ChangesBelowADotFolder_AreIgnored()
    {
        Directory.CreateDirectory(Path.Combine(folder, ".git"));
        using var service = CreateService();
        service.Watch(folder, recursive: true);

        await File.WriteAllTextAsync(Path.Combine(folder, ".git", "index"), "x", TestContext.Current.CancellationToken);
        await Task.Delay(400, TestContext.Current.CancellationToken);

        Count().Should().Be(0);
    }

    [Fact]
    public async Task Unwatched_IsNotReported()
    {
        using var service = CreateService();
        service.Watch(folder, recursive: true);
        service.Unwatch(folder + Path.DirectorySeparatorChar);

        await File.WriteAllTextAsync(Path.Combine(folder, "a.md"), "x", TestContext.Current.CancellationToken);
        await Task.Delay(400, TestContext.Current.CancellationToken);

        Count().Should().Be(0);
    }

    [Fact]
    public void MissingFolder_IsNotFound()
    {
        using var service = CreateService();

        FluentActions.Invoking(() => service.Watch(Path.Combine(folder, "nope"), recursive: true))
            .Should().Throw<BridgeException>().Which.Code.Should().Be("not_found");
    }

    [Theory]
    [InlineData("sub/a.md", false)]
    [InlineData(".git/index", true)]
    [InlineData("sub/.obsidian/x.json", true)]
    [InlineData(".hidden.md", true)]
    public void IsBelowHiddenName_ChecksEverySegment(
        string relativePath,
        bool expected)
        => FolderWatchService.IsBelowHiddenName(folder, Path.Combine(folder, relativePath)).Should().Be(expected);

    public void Dispose()
        => Directory.Delete(folder, recursive: true);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    private FolderWatchService CreateService()
        => new(
            (f, p, r) =>
            {
                lock (reports)
                {
                    reports.Add((f, p, r));
                }
            },
            TimeSpan.FromMilliseconds(150));

    private int Count()
    {
        lock (reports)
        {
            return reports.Count;
        }
    }

    private List<(string Folder, IReadOnlyList<string> Paths, bool Rescan)> Snapshot()
    {
        lock (reports)
        {
            return [.. reports];
        }
    }
}