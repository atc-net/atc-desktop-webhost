namespace Atc.Desktop.WebHost.Sample;

/// <summary>How the sample was started.</summary>
/// <param name="SelfTest">Started with <c>--selftest</c>.</param>
public sealed record SampleMode(bool SelfTest);