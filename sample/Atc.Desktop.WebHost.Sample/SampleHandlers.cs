namespace Atc.Desktop.WebHost.Sample;

/// <summary>The sample's own requests: the start mode, bridge benchmarks and a slow request to cancel.</summary>
public static class SampleHandlers
{
    /// <summary>Adds the sample's handlers.</summary>
    /// <param name="app">The app.</param>
    /// <param name="isSelfTestRun">Started with <c>--selftest</c>: the page runs the self-test at once.</param>
    public static void Register(
        WebHostApplication app,
        bool isSelfTestRun)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Bridge.Handle(
            "sample.mode",
            SampleJsonContext.Default.SampleMode,
            _ => ValueTask.FromResult(new SampleMode(isSelfTestRun)));

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

        RegisterDelay(app);
    }

    /// <summary>A slow request the page can cancel; <c>sample.delayState</c> tells whether the host saw the cancel.</summary>
    /// <param name="app">The app.</param>
    private static void RegisterDelay(WebHostApplication app)
    {
        var lastWasCancelled = false;

        app.Bridge.Handle(
            "sample.delay",
            SampleJsonContext.Default.DelayRequest,
            SampleJsonContext.Default.DelayState,
            async (request, cancellationToken) =>
            {
                lastWasCancelled = false;
                try
                {
                    await Task.Delay(Math.Clamp(request.Ms, 0, 60_000), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    lastWasCancelled = true;
                    throw;
                }

                return new DelayState(Cancelled: false);
            });

        app.Bridge.Handle(
            "sample.delayState",
            SampleJsonContext.Default.DelayState,
            _ => ValueTask.FromResult(new DelayState(lastWasCancelled)));
    }
}