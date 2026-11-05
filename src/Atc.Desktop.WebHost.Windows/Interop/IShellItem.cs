namespace Atc.Desktop.WebHost.Windows.Interop;

/// <summary>A shell item (<c>shobjidl_core.h</c>), in vtable order.</summary>
[GeneratedComInterface]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
internal partial interface IShellItem
{
    nint BindToHandler(
        nint bindContext,
        in Guid handler,
        in Guid interfaceId);

    IShellItem GetParent();

    /// <summary>Gets a name; free it with <see cref="Marshal.FreeCoTaskMem"/>.</summary>
    /// <param name="type">The kind of name, e.g. <see cref="NativeMethods.SigdnFileSysPath"/>.</param>
    /// <returns>The name, a pointer to UTF-16 text.</returns>
    nint GetDisplayName(uint type);

    uint GetAttributes(uint mask);

    int Compare(
        IShellItem other,
        uint hint);
}