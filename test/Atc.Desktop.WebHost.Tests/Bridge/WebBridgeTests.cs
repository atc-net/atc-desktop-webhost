namespace Atc.Desktop.WebHost.Tests.Bridge;

public sealed class WebBridgeTests
{
    private readonly List<string> sent = [];
    private readonly WebBridge bridge = new();

    public WebBridgeTests()
        => bridge.Connect(sent.Add);

    [Fact]
    public async Task Request_IsAnsweredWithTheHandlerResult()
    {
        bridge.Handle("math.double", (request, _) => ValueTask.FromResult(
            JsonPayload.FromRawJson((request.Payload.GetInt32() * 2).ToString(System.Globalization.CultureInfo.InvariantCulture))));

        await bridge.DispatchAsync("""{"kind":"request","id":7,"type":"math.double","payload":21}""");

        sent.Should().ContainSingle().Which.Should().Be("""{"kind":"response","id":7,"ok":true,"result":42}""");
    }

    [Fact]
    public async Task Request_WithStringId_KeepsTheIdAsIs()
    {
        bridge.Handle("ping", (_, _) => ValueTask.FromResult(JsonPayload.FromString("pong")));

        await bridge.DispatchAsync("""{"kind":"request","id":"a-1","type":"ping"}""");

        Parse(sent.Single())["id"]!.GetValue<string>().Should().Be("a-1");
    }

    [Fact]
    public async Task UnknownType_IsAnsweredWithAnError()
    {
        await bridge.DispatchAsync("""{"kind":"request","id":1,"type":"nope"}""");

        var response = Parse(sent.Single());
        response["ok"]!.GetValue<bool>().Should().BeFalse();
        response["error"]!["code"]!.GetValue<string>().Should().Be("unknown_type");
    }

    [Fact]
    public async Task TypedHandler_RejectsAMissingPayload()
    {
        bridge.Handle(
            "title",
            TestJsonContext.Default.TitleMessage,
            TestJsonContext.Default.TitleMessage,
            (message, _) => ValueTask.FromResult(message with { Title = message.Title.ToUpperInvariant() }));

        await bridge.DispatchAsync("""{"kind":"request","id":1,"type":"title"}""");
        await bridge.DispatchAsync("""{"kind":"request","id":2,"type":"title","payload":{"title":"abc"}}""");

        Parse(sent[0])["error"]!["code"]!.GetValue<string>().Should().Be("bad_request");
        Parse(sent[1])["result"]!["title"]!.GetValue<string>().Should().Be("ABC");
    }

    [Fact]
    public async Task HandlerException_IsReportedWithItsMessage()
    {
        bridge.Handle("fail", (_, _) => throw new InvalidOperationException("Boom <b>"));

        await bridge.DispatchAsync("""{"kind":"request","id":1,"type":"fail"}""");

        var response = Parse(sent.Single());
        response["error"]!["code"]!.GetValue<string>().Should().Be("handler_error");
        response["error"]!["message"]!.GetValue<string>().Should().Be("Boom <b>");
    }

    [Fact]
    public async Task Notification_WithoutId_GetsNoResponse()
    {
        var called = false;
        bridge.Handle("log", (_, _) =>
        {
            called = true;
            return ValueTask.FromResult(JsonPayload.Null);
        });

        await bridge.DispatchAsync("""{"kind":"request","type":"log"}""");

        called.Should().BeTrue();
        sent.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"kind":"other","id":1}""")]
    public async Task MalformedOrUnknownMessages_AreIgnored(string message)
    {
        await bridge.DispatchAsync(message);

        sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_CancelsTheRunningHandler()
    {
        var started = new TaskCompletionSource();
        bridge.Handle("slow", async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return JsonPayload.Null;
        });

        var running = bridge.DispatchAsync("""{"kind":"request","id":5,"type":"slow"}""");
        await started.Task;
        await bridge.DispatchAsync("""{"kind":"cancel","id":5}""");
        await running;

        Parse(sent.Single())["error"]!["code"]!.GetValue<string>().Should().Be("cancelled");
    }

    [Fact]
    public void Events_WaitUntilThePageIsReady_ThenKeepTheirOrder()
    {
        bridge.Emit("a", JsonPayload.FromRawJson("1"));
        bridge.Emit("b", JsonPayload.FromRawJson("2"));
        sent.Should().BeEmpty();

        bridge.OpenEvents();
        bridge.Emit("c", JsonPayload.FromRawJson("3"));

        sent.Select(m => Parse(m)["type"]!.GetValue<string>()).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void BuildEvent_EscapesTheType()
    {
        var message = WebBridge.BuildEvent("a\"b", JsonPayload.Null);

        Parse(message)["type"]!.GetValue<string>().Should().Be("a\"b");
    }

    private static JsonNode Parse(string json)
        => JsonNode.Parse(json)!;
}