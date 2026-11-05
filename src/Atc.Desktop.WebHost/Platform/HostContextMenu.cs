namespace Atc.Desktop.WebHost.Platform;

/// <summary>What the right-click menu shows. With dev tools on (<c>--devtools</c>) it is always <see cref="Full"/>.</summary>
public enum HostContextMenu
{
    /// <summary>No menu.</summary>
    None,

    /// <summary>Only editing: cut, copy, paste, paste as plain text, select all (and the emoji panel on Windows).</summary>
    Edit,

    /// <summary>The webview's whole menu: back, reload, save as, print, inspect, …</summary>
    Full,
}