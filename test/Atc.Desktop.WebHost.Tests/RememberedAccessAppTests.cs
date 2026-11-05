namespace Atc.Desktop.WebHost.Tests;

/// <summary>A path the user chose in an earlier run is usable again, and the page can only shrink the list.</summary>
public sealed class RememberedAccessAppTests : IDisposable
{
    private readonly string appId = "AtcWebHostTests-" + Guid.NewGuid().ToString("N");
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));
    private readonly List<string> sent = [];

    [Fact]
    public async Task PathFromAnEarlierRun_CanBeLoaded_UntilItIsForgotten()
    {
        var file = Path.Combine(folder, "tab.md");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(file, "x", TestContext.Current.CancellationToken);
        var earlier = new RememberedAccess(HostPaths.Resolve("AtcSoft", appId).ConfigFolder);
        earlier.Record(file, isFolder: false);
        earlier.Flush(TimeSpan.FromSeconds(5));

        using (var app = CreateApp(remember: true))
        {
            (await RequestAsync(app, "access.list", null))["result"]!.AsArray()
                .Select(e => e!["path"]!.GetValue<string>()).Should().Equal(file);
            (await RequestAsync(app, "document.load", file))["ok"]!.GetValue<bool>().Should().BeTrue();
            (await RequestAsync(app, "access.forget", file))["ok"]!.GetValue<bool>().Should().BeTrue();
        }

        using var next = CreateApp(remember: true);
        (await RequestAsync(next, "access.list", null))["result"]!.AsArray().Should().BeEmpty();
        (await RequestAsync(next, "document.load", file))["error"]!["code"]!.GetValue<string>().Should().Be("forbidden");
    }

    [Fact]
    public async Task RememberingOff_IgnoresTheSavedList()
    {
        var file = Path.Combine(folder, "tab.md");
        var earlier = new RememberedAccess(HostPaths.Resolve("AtcSoft", appId).ConfigFolder);
        earlier.Record(file, isFolder: false);
        earlier.Flush(TimeSpan.FromSeconds(5));

        using var app = CreateApp(remember: false);

        (await RequestAsync(app, "access.list", null))["result"]!.AsArray().Should().BeEmpty();
        (await RequestAsync(app, "document.load", file))["error"]!["code"]!.GetValue<string>().Should().Be("forbidden");
    }

    public void Dispose()
    {
        foreach (var path in (string[])[folder, HostPaths.Resolve("AtcSoft", appId).ConfigFolder, HostPaths.Resolve("AtcSoft", appId).DataFolder])
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private WebHostApplication CreateApp(bool remember)
    {
        var app = new WebHostApplication(new WebHostOptions
        {
            AppId = appId,
            Title = "Tests",
            Frontend = FrontendSource.FromFolder(AppContext.BaseDirectory),
            Platform = Substitute.For<IHostPlatform>(),
            RememberFileAccess = remember,
            LoadDocument = (path, _) => ValueTask.FromResult(JsonPayload.FromString(Path.GetFileName(path))),
        });
        app.Bridge.Connect(sent.Add);
        return app;
    }

    private async Task<JsonNode> RequestAsync(
        WebHostApplication app,
        string type,
        string? path)
    {
        sent.Clear();
        var request = new JsonObject
        {
            ["kind"] = "request",
            ["id"] = 1,
            ["type"] = type,
            ["payload"] = path is null ? null : new JsonObject { ["path"] = path },
        };
        await app.Bridge.DispatchAsync(request.ToJsonString());
        return JsonNode.Parse(sent.Single())!;
    }
}