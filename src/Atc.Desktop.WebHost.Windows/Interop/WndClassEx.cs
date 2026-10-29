namespace Atc.Desktop.WebHost.Windows.Interop;

/// <summary>WNDCLASSEXW.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WndClassEx
{
    public uint Size;
    public uint Style;
    public nint WndProc;
    public int ClassExtra;
    public int WindowExtra;
    public nint Instance;
    public nint Icon;
    public nint Cursor;
    public nint Background;
    public nint MenuName;
    public nint ClassName;
    public nint SmallIcon;
}