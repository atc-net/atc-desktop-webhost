namespace Atc.Desktop.WebHost.Platform;

/// <summary>
/// Callbacks from the platform into the host, in startup order. All are called on the UI thread.
/// </summary>
public interface IHostWindowEvents
{
    /// <summary>The native window exists and is visible (painted with the background colour). The webview is not ready yet.</summary>
    /// <param name="window">The window.</param>
    /// <param name="uiContext">Posts work to the UI thread.</param>
    void OnWindowCreated(
        IHostWindow window,
        SynchronizationContext uiContext);

    /// <summary>Records a startup milestone, e.g. "webviewEnvironmentCreated".</summary>
    /// <param name="name">The milestone name.</param>
    void Mark(string name);

    /// <summary>
    /// Builds the script that runs before any page script: it defines <c>window.atcHost</c> and the
    /// boot data (including the document when it's ready in time). Called once, before navigation.
    /// </summary>
    /// <returns>The JavaScript source.</returns>
    ValueTask<string> CreateDocumentScriptAsync();

    /// <summary>A JSON message arrived from the page.</summary>
    /// <param name="message">The message text.</param>
    void OnMessage(string message);

    /// <summary>The window is closing. The webview may already be gone.</summary>
    void OnClosed();
}