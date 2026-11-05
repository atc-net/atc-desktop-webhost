namespace Atc.Desktop.WebHost.Windows.Interop;

/// <summary>WINDOWPLACEMENT: a window's normal (restored) position and whether it is maximized.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WindowPlacementData
{
    public uint Length;
    public uint Flags;
    public uint ShowCommand;
    public Point MinPosition;
    public Point MaxPosition;
    public Rect NormalPosition;
}