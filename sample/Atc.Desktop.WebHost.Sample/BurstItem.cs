namespace Atc.Desktop.WebHost.Sample;

/// <summary>One burst event.</summary>
/// <param name="Index">The event number.</param>
/// <param name="Data">The filler payload.</param>
public sealed record BurstItem(
    int Index,
    string Data);