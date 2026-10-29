namespace Atc.Desktop.WebHost.Sample;

/// <summary>How long the host took to send the burst.</summary>
/// <param name="Count">The number of events sent.</param>
/// <param name="HostMs">Host-side send time.</param>
public sealed record BurstResult(
    int Count,
    double HostMs);