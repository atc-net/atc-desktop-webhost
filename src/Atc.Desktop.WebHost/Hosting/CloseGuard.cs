namespace Atc.Desktop.WebHost.Hosting;

/// <summary>
/// Keeps the window open while the page has unsaved work. The page arms the guard; when the user closes
/// the window, the page is asked instead (it shows its own "Save changes?" and then closes the window).
/// <para>
/// A page that has hung must never leave the window unclosable: the page's script acknowledges the
/// question at once, and a close attempt after <see cref="AnswerTimeout"/> without an acknowledgement
/// closes the window. A reload, a navigation or a crashed web process disarms the guard.
/// </para>
/// Used on the UI thread only.
/// </summary>
internal sealed class CloseGuard
{
    /// <summary>How long a question may stay unacknowledged before the next close attempt closes anyway.</summary>
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(3);

    private readonly TimeProvider time;
    private DateTimeOffset? askedAt;

    public CloseGuard(TimeProvider time)
        => this.time = time;

    /// <summary>Gets a value indicating whether the page asked to be consulted before the window closes.</summary>
    public bool IsArmed { get; private set; }

    /// <summary>Arms or disarms the guard. Either way, the page is alive, so a pending question is answered.</summary>
    /// <param name="armed"><see langword="true"/> while the page has unsaved work.</param>
    public void SetArmed(bool armed)
    {
        IsArmed = armed;
        askedAt = null;
    }

    /// <summary>The page's script received the question.</summary>
    public void Acknowledge()
        => askedAt = null;

    /// <summary>The page is gone (navigation, reload or a crashed web process).</summary>
    public void Reset()
        => SetArmed(armed: false);

    /// <summary>The user tries to close the window.</summary>
    /// <returns>What to do.</returns>
    public CloseDecision OnUserClose()
    {
        if (!IsArmed)
        {
            return CloseDecision.Close;
        }

        var now = time.GetUtcNow();
        if (askedAt is null)
        {
            askedAt = now;
            return CloseDecision.AskPage;
        }

        return now - askedAt.Value >= AnswerTimeout
            ? CloseDecision.Close
            : CloseDecision.Wait;
    }
}