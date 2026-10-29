namespace Atc.Desktop.WebHost.Sample;

/// <summary>Asks the host to send <paramref name="Count"/> events of <paramref name="Size"/> characters.</summary>
/// <param name="Count">The number of events.</param>
/// <param name="Size">The payload size per event.</param>
public sealed record BurstRequest(
    int Count,
    int Size);