namespace Atc.Desktop.WebHost.Platform;

/// <summary>A rectangle in physical screen pixels.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct HostRect(
    int X,
    int Y,
    int Width,
    int Height);