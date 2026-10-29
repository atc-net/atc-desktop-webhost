namespace Atc.Desktop.WebHost.Windows.Interop;

/// <summary>OPENFILENAMEW.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct OpenFileName
{
    public uint StructSize;
    public nint Owner;
    public nint Instance;
    public nint Filter;
    public nint CustomFilter;
    public uint MaxCustomFilter;
    public uint FilterIndex;
    public nint File;
    public uint MaxFile;
    public nint FileTitle;
    public uint MaxFileTitle;
    public nint InitialDir;
    public nint Title;
    public uint Flags;
    public ushort FileOffset;
    public ushort FileExtension;
    public nint DefaultExtension;
    public nint CustomData;
    public nint Hook;
    public nint TemplateName;
    public nint Reserved;
    public uint ReservedFlags;
    public uint FlagsEx;
}