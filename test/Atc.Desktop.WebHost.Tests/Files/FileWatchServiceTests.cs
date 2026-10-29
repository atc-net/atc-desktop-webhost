namespace Atc.Desktop.WebHost.Tests.Files;

public sealed class FileWatchServiceTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));

    public FileWatchServiceTests()
        => Directory.CreateDirectory(folder);

    [Fact]
    public async Task SeveralWrites_AreReportedOnce()
    {
        var path = Path.Combine(folder, "doc.md");
        await File.WriteAllTextAsync(path, "1", TestContext.Current.CancellationToken);
        var reports = new List<(string Path, string Kind)>();
        using var service = new FileWatchService(
            (p, k) =>
            {
                lock (reports)
                {
                    reports.Add((p, k));
                }
            },
            TimeSpan.FromMilliseconds(150));
        service.Watch(path);

        for (var i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(path, i.ToString(System.Globalization.CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);
        }

        await WaitUntilAsync(() => reports.Count > 0);
        await Task.Delay(400, TestContext.Current.CancellationToken);

        reports.Should().ContainSingle().Which.Should().Be((Path.GetFullPath(path), "changed"));
    }

    [Fact]
    public async Task AtomicSave_IsReportedAsChanged()
    {
        var path = Path.Combine(folder, "doc.md");
        await File.WriteAllTextAsync(path, "old", TestContext.Current.CancellationToken);
        var reports = new List<(string Path, string Kind)>();
        using var service = new FileWatchService(
            (p, k) =>
            {
                lock (reports)
                {
                    reports.Add((p, k));
                }
            },
            TimeSpan.FromMilliseconds(150));
        service.Watch(path);

        var temp = Path.Combine(folder, "doc.md.tmp");
        await File.WriteAllTextAsync(temp, "new", TestContext.Current.CancellationToken);
        File.Move(temp, path, overwrite: true);

        await WaitUntilAsync(() => reports.Count > 0);

        reports.Should().ContainSingle().Which.Kind.Should().Be("changed");
    }

    [Fact]
    public async Task OtherFilesInTheFolder_AreIgnored()
    {
        var path = Path.Combine(folder, "doc.md");
        await File.WriteAllTextAsync(path, "1", TestContext.Current.CancellationToken);
        var reports = new List<string>();
        using var service = new FileWatchService(
            (p, _) =>
            {
                lock (reports)
                {
                    reports.Add(p);
                }
            },
            TimeSpan.FromMilliseconds(50));
        service.Watch(path);

        await File.WriteAllTextAsync(Path.Combine(folder, "other.md"), "x", TestContext.Current.CancellationToken);
        await Task.Delay(400, TestContext.Current.CancellationToken);

        reports.Should().BeEmpty();
    }

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
}