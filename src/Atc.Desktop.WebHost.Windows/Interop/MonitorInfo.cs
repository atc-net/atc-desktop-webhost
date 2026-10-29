namespace Atc.Desktop.WebHost.Windows.Interop;

/// <summary>MONITORINFO.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MonitorInfo
{
    public uint Size;
    public Rect Monitor;
    public Rect Work;
    public uint Flags;
}