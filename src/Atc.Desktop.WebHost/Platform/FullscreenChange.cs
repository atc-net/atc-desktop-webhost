namespace Atc.Desktop.WebHost.Platform;

/// <summary>What a fullscreen event should do with the window.</summary>
internal enum FullscreenChange
{
    /// <summary>Leave the window as it is.</summary>
    None,

    /// <summary>Make the window fullscreen.</summary>
    Enter,

    /// <summary>Bring the window back to its earlier size and place.</summary>
    Leave,
}