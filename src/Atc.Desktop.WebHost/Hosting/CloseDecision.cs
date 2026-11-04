namespace Atc.Desktop.WebHost.Hosting;

/// <summary>What to do when the user tries to close the window.</summary>
internal enum CloseDecision
{
    /// <summary>Close the window.</summary>
    Close,

    /// <summary>Keep the window open and ask the page (send <c>window.closeRequested</c>).</summary>
    AskPage,

    /// <summary>Keep the window open: the page was asked a moment ago and hasn't answered yet.</summary>
    Wait,
}