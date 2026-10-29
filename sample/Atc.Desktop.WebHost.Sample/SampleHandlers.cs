namespace Atc.Desktop.WebHost.Sample;

/// <summary>The sample's own requests: bridge benchmarks.</summary>
public static class SampleHandlers
{
    /// <summary>Adds the sample's handlers.</summary>
    /// <param name="app">The app.</param>
    public static void Register(WebHostApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Round trip: the page measures request → response latency with any payload size.
        app.Bridge.Handle("sample.echo", (request, _) => ValueTask.FromResult(
            request.Payload.ValueKind == JsonValueKind.Undefined
                ? JsonPayload.Null
                : JsonPayload.FromRawJson(request.Payload.GetRawText())));

        // Throughput: the host sends a burst of events; the page measures how fast they arrive.
        app.Bridge.Handle(
            "sample.burst",
            SampleJsonContext.Default.BurstRequest,
            SampleJsonContext.Default.BurstResult,
            (request, _) =>
            {
                var started = Stopwatch.GetTimestamp();
                var data = new string('x', Math.Clamp(request.Size, 0, 10_000_000));
                var count = Math.Clamp(request.Count, 0, 100_000);
                for (var i = 0; i < count; i++)
                {
                    app.Bridge.Emit("sample.burstItem", new BurstItem(i, data), SampleJsonContext.Default.BurstItem);
                }

                return ValueTask.FromResult(new BurstResult(count, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
            });
    }
}