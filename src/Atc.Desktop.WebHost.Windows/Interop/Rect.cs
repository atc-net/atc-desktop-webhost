namespace Atc.Desktop.WebHost.Windows.Interop;

/// <summary>RECT.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly int Width
        => Right - Left;

    public readonly int Height
        => Bottom - Top;
}