namespace Atc.Desktop.WebHost.Platform;

/// <summary>
/// The native window as the host sees it. Kept deliberately small: only what viewer apps need.
/// All members must be called on the UI thread.
/// </summary>
public interface IHostWindow
{
    /// <summary>Sends one JSON message to the page (delivered to <c>atcHost</c>).</summary>
    /// <param name="json">The JSON message.</param>
    void PostMessage(string json);

    /// <summary>Sets the window title.</summary>
    /// <param name="title">The title.</param>
    void SetTitle(string title);

    /// <summary>Restores the window if minimized and brings it to the front.</summary>
    void BringToFront();

    /// <summary>Closes the window, which ends <see cref="IHostPlatform.Run"/>.</summary>
    void Close();

    /// <summary>Shows the native Open dialog.</summary>
    /// <param name="options">Title, start folder and filters.</param>
    /// <returns>The picked path, or <see langword="null"/> when cancelled.</returns>
    Task<string?> ShowOpenFileDialogAsync(FileDialogOptions options);

    /// <summary>Shows the native Open Folder dialog.</summary>
    /// <param name="options">The title and the folder to start in; filters are ignored.</param>
    /// <returns>The chosen folder, or <see langword="null"/> when cancelled.</returns>
    Task<string?> ShowOpenFolderDialogAsync(FileDialogOptions options);

    /// <summary>Shows the native Save dialog.</summary>
    /// <param name="options">Title, suggested path and filters.</param>
    /// <returns>The picked path, or <see langword="null"/> when cancelled.</returns>
    Task<string?> ShowSaveFileDialogAsync(FileDialogOptions options);

    /// <summary>Gets the ids of the webview's own processes (browser, renderer, GPU, ...), for memory reports.</summary>
    /// <returns>The process ids; empty when unknown.</returns>
    IReadOnlyList<int> GetBrowserProcessIds();

    /// <summary>Gets where the window is now: display, fullscreen and taskbar (changed by F11 too).</summary>
    WindowPlacement Placement { get; }

    /// <summary>Moves the window to a display, into or out of fullscreen. See <see cref="WindowPlacement"/>.</summary>
    /// <param name="placement">Where the window goes.</param>
    void SetPlacement(WindowPlacement placement);

    /// <summary>Opens the webview's find bar (Windows). Does nothing where there is none (Linux).</summary>
    /// <param name="text">The text to search for, or <see langword="null"/> for an empty find bar.</param>
    void Find(string? text);

    /// <summary>Opens the print dialog for the page.</summary>
    void Print();
}