namespace Atc.Desktop.WebHost.Tests.Platform;

/// <summary>
/// Checks the Open Folder dialog's COM declarations against Windows without showing it: a method in the wrong
/// vtable slot would crash the app on the first click. Showing and picking needs a person.
/// </summary>
public sealed class WindowsFolderDialogTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));

    public WindowsFolderDialogTests()
        => Directory.CreateDirectory(folder);

    [Fact]
    public void Dialog_TakesFolderOptions_AndReportsTheFolderItWillOpenIn()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var (result, failure) = RunOnStaThread(folder);

        failure.Should().BeNull();
        (result.Options & NativeMethods.FosPickFolders).Should().Be(NativeMethods.FosPickFolders);
        result.Folder.Should().BeEquivalentTo(folder);
    }

    public void Dispose()
        => Directory.Delete(folder, recursive: true);

    /// <summary>The dialog needs a single-threaded apartment, like the window's thread.</summary>
    [SupportedOSPlatform("windows")]
    private static ((uint Options, string? Folder) Result, COMException? Failure) RunOnStaThread(
        string folder)
    {
        (uint Options, string? Folder) result = default;
        COMException? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = UseDialog(folder);
            }
            catch (COMException ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return (result, failure);
    }

    [SupportedOSPlatform("windows")]
    private static (uint Options, string? Folder) UseDialog(string folder)
    {
        Marshal.ThrowExceptionForHR(NativeMethods.CoCreateInstance(
            NativeMethods.FileOpenDialogClassId,
            outer: 0,
            NativeMethods.ClsctxInprocServer,
            typeof(IFileDialog).GUID,
            out var dialog));
        dialog.SetOptions(dialog.GetOptions() | NativeMethods.FosPickFolders | NativeMethods.FosForceFileSystem);
        dialog.SetTitle("Test");

        Marshal.ThrowExceptionForHR(NativeMethods.SHCreateItemFromParsingName(folder, 0, typeof(IShellItem).GUID, out var item));
        dialog.SetFolder(item);

        var name = dialog.GetFolder().GetDisplayName(NativeMethods.SigdnFileSysPath);
        try
        {
            return (dialog.GetOptions(), Marshal.PtrToStringUni(name));
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }
}