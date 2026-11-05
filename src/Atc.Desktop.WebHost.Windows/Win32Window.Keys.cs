namespace Atc.Desktop.WebHost.Windows;

/// <content>Standard keys (<see cref="HostKeys"/>), find and print.</content>
internal sealed partial class Win32Window
{
    public void Find(string? text)
    {
        if (webView is null || environment is null)
        {
            return;
        }

        var findOptions = environment.CreateFindOptions();
        findOptions.FindTerm = text ?? string.Empty;
        findOptions.ShouldHighlightAllMatches = true;
        _ = webView.Find.StartAsync(findOptions);
    }

    public void Print()
        => webView?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);

    /// <summary>
    /// Runs before the page sees the key. Only the first key-down counts (holding F11 would otherwise flicker), and
    /// the action runs after this event handler returns, like the dialogs.
    /// </summary>
    private void OnAcceleratorKeyPressed(
        object? sender,
        CoreWebView2AcceleratorKeyPressedEventArgs e)
    {
        if (e.KeyEventKind is not (CoreWebView2KeyEventKind.KeyDown or CoreWebView2KeyEventKind.SystemKeyDown) ||
            e.PhysicalKeyStatus.WasKeyDown != 0)
        {
            return;
        }

        var key = (int)e.VirtualKey;
        if (key == NativeMethods.VkEscape && webView?.ContainsFullScreenElement == true)
        {
            // Browser keys are off, so make sure Esc still ends a video's fullscreen. The page sees Esc too.
            _ = webView.ExecuteScriptAsync("document.exitFullscreen()");
            return;
        }

        var action = ActionFor(key);
        if (action is null)
        {
            return;
        }

        e.Handled = true;
        uiContext?.Post(static state => ((Action)state!)(), action);
    }

    private Action? ActionFor(int key)
    {
        var ctrl = IsDown(NativeMethods.VkControl);
        var alt = IsDown(NativeMethods.VkMenu); // Ctrl+Alt is AltGr on many keyboards: leave it to the page.
        var keys = options.Keys;
        return key switch
        {
            NativeMethods.VkF11 when !ctrl && !alt && keys.HasFlag(HostKeys.Fullscreen) => ToggleFullscreen,
            NativeMethods.VkF when ctrl && !alt && keys.HasFlag(HostKeys.Find) => () => Find(text: null),
            NativeMethods.VkP when ctrl && !alt && keys.HasFlag(HostKeys.Print) => Print,
            NativeMethods.VkF4 when ctrl && !alt && keys.HasFlag(HostKeys.Close) => CloseAsUser,
            _ => null,
        };
    }

    /// <summary>Ctrl+F4 closes like the title-bar button: the close guard is asked.</summary>
    private void CloseAsUser()
        => NativeMethods.PostMessageW(hwnd, NativeMethods.WmClose, 0, 0);

    private static bool IsDown(int virtualKey)
        => (NativeMethods.GetKeyState(virtualKey) & 0x8000) != 0;
}