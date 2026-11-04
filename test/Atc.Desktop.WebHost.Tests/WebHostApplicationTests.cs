namespace Atc.Desktop.WebHost.Tests;

public sealed class WebHostApplicationTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));
    private readonly List<string> sent = [];
    private readonly WebHostApplication app;

    public WebHostApplicationTests()
    {
        Directory.CreateDirectory(folder);
        app = new WebHostApplication(new WebHostOptions
        {
            AppId = "AtcWebHostTests",
            Title = "Tests",
            Frontend = FrontendSource.FromFolder(AppContext.BaseDirectory),
            Platform = Substitute.For<IHostPlatform>(),
            LoadDocument = (path, _) => ValueTask.FromResult(JsonPayload.FromString(Path.GetFileName(path))),
        });
        app.Bridge.Connect(sent.Add);
    }

    [Fact]
    public async Task DocumentLoad_OutsideTheOpenedFolders_IsForbidden()
    {
        await app.Bridge.DispatchAsync(LoadRequest(Path.Combine(folder, "secret.txt")));

        var response = JsonNode.Parse(sent.Single())!;
        response["ok"]!.GetValue<bool>().Should().BeFalse();
        response["error"]!["code"]!.GetValue<string>().Should().Be("forbidden");
    }

    [Fact]
    public async Task DocumentLoad_NextToAGrantedFile_IsLoaded()
    {
        app.GrantFileAccess(Path.Combine(folder, "readme.md"));

        await app.Bridge.DispatchAsync(LoadRequest(Path.Combine(folder, "other.md")));

        var response = JsonNode.Parse(sent.Single())!;
        response["ok"]!.GetValue<bool>().Should().BeTrue();
        response["result"]!.GetValue<string>().Should().Be("other.md");
    }

    [Fact]
    public async Task FileWatch_OutsideTheOpenedFolders_IsForbidden()
    {
        var request = new JsonObject
        {
            ["kind"] = "request",
            ["id"] = 1,
            ["type"] = "file.watch",
            ["payload"] = new JsonObject { ["path"] = Path.Combine(folder, "secret.txt") },
        };

        await app.Bridge.DispatchAsync(request.ToJsonString());

        JsonNode.Parse(sent.Single())!["error"]!["code"]!.GetValue<string>().Should().Be("forbidden");
    }

    [Fact]
    public void UserClose_WithoutCloseGuard_Closes()
        => ((IHostWindowEvents)app).OnCloseRequested().Should().BeTrue();

    [Fact]
    public async Task UserClose_WithCloseGuard_KeepsTheWindowAndAsksThePage()
    {
        await app.Bridge.DispatchAsync(Request("window.setCloseGuard", new JsonObject { ["enabled"] = true }));
        await app.Bridge.DispatchAsync(Request("host.ready", payload: null));
        sent.Clear();

        var closes = ((IHostWindowEvents)app).OnCloseRequested();

        closes.Should().BeFalse();
        JsonNode.Parse(sent.Single())!["type"]!.GetValue<string>().Should().Be("window.closeRequested");
    }

    [Fact]
    public async Task UserClose_AfterThePageUnloaded_Closes()
    {
        await app.Bridge.DispatchAsync(Request("window.setCloseGuard", new JsonObject { ["enabled"] = true }));

        ((IHostWindowEvents)app).OnPageUnloaded();

        ((IHostWindowEvents)app).OnCloseRequested().Should().BeTrue();
    }

    public void Dispose()
    {
        app.Dispose();
        Directory.Delete(folder, recursive: true);
    }

    private static string Request(
        string type,
        JsonNode? payload)
        => new JsonObject
        {
            ["kind"] = "request",
            ["id"] = 1,
            ["type"] = type,
            ["payload"] = payload,
        }.ToJsonString();

    private static string LoadRequest(string path)
        => new JsonObject
        {
            ["kind"] = "request",
            ["id"] = 1,
            ["type"] = "document.load",
            ["payload"] = new JsonObject { ["path"] = path },
        }.ToJsonString();
}