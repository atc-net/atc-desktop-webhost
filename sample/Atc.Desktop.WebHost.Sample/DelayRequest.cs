namespace Atc.Desktop.WebHost.Sample;

/// <summary>A request to wait.</summary>
/// <param name="Ms">How long, in milliseconds.</param>
public sealed record DelayRequest(int Ms);