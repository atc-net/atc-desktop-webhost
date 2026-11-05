namespace Atc.Desktop.WebHost.Windows.Interop;

/// <summary>
/// The common item dialog (<c>shobjidl_core.h</c>), used for the Open Folder dialog. The methods are in the
/// header's order, which is the COM vtable order: <c>IModalWindow.Show</c>, then <c>IFileDialog</c>.
/// </summary>
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
internal partial interface IFileDialog
{
    /// <summary>Shows the dialog. Returns <see cref="NativeMethods.ErrorCancelled"/> as an HRESULT when cancelled.</summary>
    /// <param name="owner">The owner window.</param>
    /// <returns>The HRESULT.</returns>
    [PreserveSig]
    int Show(nint owner);

    void SetFileTypes(
        uint count,
        nint filterSpecs);

    void SetFileTypeIndex(uint index);

    uint GetFileTypeIndex();

    uint Advise(nint events);

    void Unadvise(uint cookie);

    void SetOptions(uint options);

    uint GetOptions();

    void SetDefaultFolder(IShellItem item);

    void SetFolder(IShellItem item);

    IShellItem GetFolder();

    IShellItem GetCurrentSelection();

    void SetFileName(string name);

    nint GetFileName();

    void SetTitle(string title);

    void SetOkButtonLabel(string text);

    void SetFileNameLabel(string label);

    IShellItem GetResult();

    void AddPlace(
        IShellItem item,
        int place);

    void SetDefaultExtension(string extension);

    void Close(int result);

    void SetClientGuid(in Guid guid);

    void ClearClientData();

    void SetFilter(nint filter);
}