namespace Atc.Desktop.WebHost.Sample;

/// <summary>Whether the last <c>sample.delay</c> was cancelled by the page.</summary>
/// <param name="Cancelled">The host saw the cancel.</param>
public sealed record DelayState(bool Cancelled);