namespace Atc.Desktop.WebHost.Windows.Interop;

/// <summary>
/// The Win32 calls the window needs. Source-generated (<see cref="LibraryImportAttribute"/>), so they
/// are precompiled with ReadyToRun and work with Native AOT.
/// Modelled on the calls Photino.Native makes in <c>Photino.Windows.cpp</c> (Apache-2.0).
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class NativeMethods
{
    public const uint WmMove = 0x0003;
    public const uint WmSize = 0x0005;
    public const uint WmSetFocus = 0x0007;
    public const uint WmClose = 0x0010;
    public const uint WmDestroy = 0x0002;
    public const uint WmGetMinMaxInfo = 0x0024;
    public const uint WmDpiChanged = 0x02E0;
    public const uint WmApp = 0x8000;

    public const uint CsHRedraw = 0x0002;
    public const uint CsVRedraw = 0x0001;
    public const uint WsOverlappedWindow = 0x00CF0000;
    public const int CwUseDefault = unchecked((int)0x80000000);
    public const int SwShowNormal = 1;
    public const int SwRestore = 9;
    public const int SizeMinimized = 1;
    public const uint SwpNoZOrder = 0x0004;
    public const uint SwpNoActivate = 0x0010;
    public const uint MonitorDefaultToNearest = 0x00000002;
    public const uint AsfwAny = unchecked((uint)-1);
    public const uint MbIconError = 0x00000010;
    public const nint IdcArrow = 32512;
    public const nint DpiAwarenessContextPerMonitorAwareV2 = -4;
    public const uint DwmwaUseImmersiveDarkMode = 20;

    public const uint WsPopup = 0x80000000;
    public const uint WsVisible = 0x10000000;
    public const uint WsExToolWindow = 0x00000080;
    public const int GwlStyle = -16;
    public const int GwlExStyle = -20;
    public const int SwHide = 0;
    public const int SwShow = 5;
    public const uint SwpNoSize = 0x0001;
    public const uint SwpFrameChanged = 0x0020;
    public const uint MonitorInfoPrimary = 0x00000001;
    public const int MdtEffectiveDpi = 0;
    public const uint SwpNoMove = 0x0002;
    public const int VkF4 = 0x73;
    public const int VkF11 = 0x7A;
    public const int VkF = 0x46;
    public const int VkP = 0x50;
    public const int VkEscape = 0x1B;
    public const int VkControl = 0x11;
    public const int VkMenu = 0x12;

    public const uint OfnOverwritePrompt = 0x00000002;
    public const uint OfnNoChangeDir = 0x00000008;
    public const uint OfnPathMustExist = 0x00000800;
    public const uint OfnFileMustExist = 0x00001000;
    public const uint OfnExplorer = 0x00080000;

    public const uint FosNoChangeDir = 0x00000008;
    public const uint FosPickFolders = 0x00000020;
    public const uint FosForceFileSystem = 0x00000040;
    public const uint FosPathMustExist = 0x00000800;
    public const uint SigdnFileSysPath = 0x80058000;
    public const uint ClsctxInprocServer = 0x1;
    public const int ErrorCancelled = unchecked((int)0x800704C7);

    public static readonly Guid FileOpenDialogClassId = new("dc1c5a9c-e88a-4dde-a5a1-60f82a20aef7");

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial ushort RegisterClassExW(in WndClassEx windowClass);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(
        uint exStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint param);

    [LibraryImport("user32.dll")]
    public static partial nint DefWindowProcW(
        nint hwnd,
        uint message,
        nint wParam,
        nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(
        nint hwnd,
        int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial int GetMessageW(
        out Msg message,
        nint hwnd,
        uint filterMin,
        uint filterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(in Msg message);

    [LibraryImport("user32.dll")]
    public static partial nint DispatchMessageW(in Msg message);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessageW(
        nint hwnd,
        uint message,
        nint wParam,
        nint lParam);

    [LibraryImport("user32.dll")]
    public static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(
        nint hwnd,
        out Rect rect);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowTextW(
        nint hwnd,
        string text);

    [LibraryImport("user32.dll")]
    public static partial nint LoadCursorW(
        nint instance,
        nint cursorName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessDpiAwarenessContext(nint value);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(
        nint hwnd,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(
        nint hwnd,
        uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfoW(
        nint monitor,
        ref MonitorInfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AdjustWindowRectExForDpi(
        ref Rect rect,
        uint style,
        [MarshalAs(UnmanagedType.Bool)] bool menu,
        uint exStyle,
        uint dpi);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AllowSetForegroundWindow(uint processId);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBoxW(
        nint hwnd,
        string text,
        string caption,
        uint type);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint icon);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint handle);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(
        nint hwnd,
        uint attribute,
        ref int value,
        uint size);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? moduleName);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint ExtractIconExW(
        string file,
        int index,
        out nint largeIcon,
        out nint smallIcon,
        uint count);

    [LibraryImport("comdlg32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetOpenFileNameW(ref OpenFileName openFileName);

    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(
        in Guid classId,
        nint outer,
        uint context,
        in Guid interfaceId,
        out IFileDialog dialog);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SHCreateItemFromParsingName(
        string path,
        nint bindContext,
        in Guid interfaceId,
        out IShellItem item);

    [LibraryImport("comdlg32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetSaveFileNameW(ref OpenFileName openFileName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool EnumDisplayMonitors(
        nint hdc,
        nint clip,
        delegate* unmanaged<nint, nint, nint, nint, int> callback,
        nint data);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(
        nint monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(
        nint hwnd,
        int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(
        nint hwnd,
        int index,
        nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowPlacement(
        nint hwnd,
        ref WindowPlacementData placement);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPlacement(
        nint hwnd,
        in WindowPlacementData placement);

    [LibraryImport("user32.dll")]
    public static partial short GetKeyState(int virtualKey);
}