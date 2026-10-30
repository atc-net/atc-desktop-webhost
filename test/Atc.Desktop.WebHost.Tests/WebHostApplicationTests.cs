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

    public void Dispose()
    {
        app.Dispose();
        Directory.Delete(folder, recursive: true);
    }

    private static string LoadRequest(string path)
        => new JsonObject
        {
            ["kind"] = "request",
            ["id"] = 1,
            ["type"] = "document.load",
            ["payload"] = new JsonObject { ["path"] = path },
        }.ToJsonString();
}