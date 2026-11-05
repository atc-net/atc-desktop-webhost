namespace Atc.Desktop.WebHost.Windows;

/// <content>
/// Native Open and Save dialogs (comdlg32; Windows shows the modern Explorer dialog), and the Open Folder dialog
/// (the common item dialog, which comdlg32 has no folder mode for).
/// </content>
internal sealed partial class Win32Window
{
    private const int MaxPath = 32_768;

    public Task<string?> ShowOpenFileDialogAsync(FileDialogOptions options)
        => Task.FromResult(ShowFileDialog(options, save: false));

    public Task<string?> ShowSaveFileDialogAsync(FileDialogOptions options)
        => Task.FromResult(ShowFileDialog(options, save: true));

    public Task<string?> ShowOpenFolderDialogAsync(FileDialogOptions options)
        => Task.FromResult(ShowFolderDialog(options));

    /// <summary>Builds the comdlg32 filter: "Name\0*.a;*.b\0...\0\0".</summary>
    /// <param name="filters">The filters; "All files" is added last.</param>
    /// <returns>The filter text.</returns>
    internal static string BuildFilter(IReadOnlyList<FileDialogFilter> filters)
    {
        var sb = new StringBuilder();
        foreach (var filter in filters.Concat([new FileDialogFilter("All files", ["*.*"])]))
        {
            var patterns = string.Join(';', filter.Patterns);
            sb.Append(filter.Name).Append(" (").Append(patterns).Append(")\0").Append(patterns).Append('\0');
        }

        return sb.Append('\0').ToString();
    }

    private string? ShowFolderDialog(FileDialogOptions dialogOptions)
    {
        ArgumentNullException.ThrowIfNull(dialogOptions);

        Marshal.ThrowExceptionForHR(NativeMethods.CoCreateInstance(
            NativeMethods.FileOpenDialogClassId,
            outer: 0,
            NativeMethods.ClsctxInprocServer,
            typeof(IFileDialog).GUID,
            out var dialog));
        dialog.SetOptions(dialog.GetOptions() | NativeMethods.FosPickFolders | NativeMethods.FosForceFileSystem |
                          NativeMethods.FosPathMustExist | NativeMethods.FosNoChangeDir);
        if (dialogOptions.Title is { Length: > 0 } title)
        {
            dialog.SetTitle(title);
        }

        if (dialogOptions.DefaultPath is { Length: > 0 } defaultPath && Directory.Exists(defaultPath) &&
            NativeMethods.SHCreateItemFromParsingName(defaultPath, 0, typeof(IShellItem).GUID, out var folder) == 0)
        {
            dialog.SetFolder(folder);
        }

        var result = dialog.Show(hwnd);
        if (result == NativeMethods.ErrorCancelled)
        {
            return null;
        }

        Marshal.ThrowExceptionForHR(result);
        var name = dialog.GetResult().GetDisplayName(NativeMethods.SigdnFileSysPath);
        try
        {
            return Marshal.PtrToStringUni(name);
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    private unsafe string? ShowFileDialog(
        FileDialogOptions dialogOptions,
        bool save)
    {
        ArgumentNullException.ThrowIfNull(dialogOptions);

        var buffer = new char[MaxPath];
        string? initialFolder = null;
        if (dialogOptions.DefaultPath is { Length: > 0 } defaultPath)
        {
            if (save || File.Exists(defaultPath))
            {
                Path.GetFileName(defaultPath.AsSpan()).CopyTo(buffer);
                initialFolder = Path.GetDirectoryName(defaultPath);
            }
            else
            {
                initialFolder = defaultPath;
            }
        }

        var filter = BuildFilter(dialogOptions.Filters);
        fixed (char* file = buffer)
        {
            fixed (char* filterText = filter)
            {
                fixed (char* title = dialogOptions.Title)
                {
                    fixed (char* folder = initialFolder)
                    {
                        return ShowFileDialog(file, filterText, title, folder, save);
                    }
                }
            }
        }
    }

    private unsafe string? ShowFileDialog(
        char* file,
        char* filterText,
        char* title,
        char* folder,
        bool save)
    {
        var dialog = new OpenFileName
        {
            StructSize = (uint)sizeof(OpenFileName),
            Owner = hwnd,
            Filter = (nint)filterText,
            FilterIndex = 1,
            File = (nint)file,
            MaxFile = MaxPath,
            Title = (nint)title,
            InitialDir = (nint)folder,
            Flags = NativeMethods.OfnExplorer | NativeMethods.OfnNoChangeDir | NativeMethods.OfnPathMustExist |
                    (save ? NativeMethods.OfnOverwritePrompt : NativeMethods.OfnFileMustExist),
        };

        var picked = save
            ? NativeMethods.GetSaveFileNameW(ref dialog)
            : NativeMethods.GetOpenFileNameW(ref dialog);
        return picked ? new string(file) : null;
    }
}