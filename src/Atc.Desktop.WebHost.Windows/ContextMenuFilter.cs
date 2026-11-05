namespace Atc.Desktop.WebHost.Windows;

/// <summary>Trims the webview's right-click menu to editing (<see cref="HostContextMenu.Edit"/>).</summary>
[SupportedOSPlatform("windows")]
internal static class ContextMenuFilter
{
    // Names as WebView2 reports them (checked against the runtime).
    private static readonly HashSet<string> EditItems = new(StringComparer.Ordinal)
    {
        "cut", "copy", "paste", "pasteAndMatchStyle", "selectAll", "emoji", "copyLinkLocation", "copyImage",
    };

    public static void KeepEditItems(
        CoreWebView2ContextMenuRequestedEventArgs e)
    {
        var items = e.MenuItems;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i].Kind != CoreWebView2ContextMenuItemKind.Separator && !EditItems.Contains(items[i].Name))
            {
                items.RemoveAt(i);
            }
        }

        // No separator at the start, at the end or twice in a row.
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var isSeparator = items[i].Kind == CoreWebView2ContextMenuItemKind.Separator;
            var isEdge = i == 0 || i == items.Count - 1;
            var followsSeparator = i > 0 && items[i - 1].Kind == CoreWebView2ContextMenuItemKind.Separator;
            if (isSeparator && (isEdge || followsSeparator))
            {
                items.RemoveAt(i);
            }
        }

        // Nothing left (e.g. a right-click on empty page space): no menu at all.
        e.Handled = items.Count == 0;
    }
}