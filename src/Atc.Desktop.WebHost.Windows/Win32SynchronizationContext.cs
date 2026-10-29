namespace Atc.Desktop.WebHost.Windows;

/// <summary>
/// Runs posted work on the window's thread. WebView2 is a single-threaded COM object, so every
/// <c>await</c> in the host must resume there. Work is queued and the window is woken with one
/// private message; the message loop then runs everything queued.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class Win32SynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();
    private readonly nint hwnd;
    private readonly uint wakeMessage;
    private readonly int threadId;
    private int signaled;

    public Win32SynchronizationContext(
        nint hwnd,
        uint wakeMessage)
    {
        this.hwnd = hwnd;
        this.wakeMessage = wakeMessage;
        threadId = Environment.CurrentManagedThreadId;
    }

    public override void Post(
        SendOrPostCallback d,
        object? state)
    {
        queue.Enqueue((d, state));
        if (Interlocked.Exchange(ref signaled, 1) == 0)
        {
            NativeMethods.PostMessageW(hwnd, wakeMessage, 0, 0);
        }
    }

    public override void Send(
        SendOrPostCallback d,
        object? state)
    {
        if (Environment.CurrentManagedThreadId == threadId)
        {
            d(state);
            return;
        }

        using var done = new ManualResetEventSlim();
        ExceptionDispatchInfo? error = null;
        Post(
            _ =>
            {
                try
                {
                    d(state);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    error = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    done.Set();
                }
            },
            state: null);
        done.Wait();
        error?.Throw();
    }

    public override SynchronizationContext CreateCopy()
        => this;

    /// <summary>Runs everything queued. Called by the window for the wake message.</summary>
    public void Drain()
    {
        Volatile.Write(ref signaled, 0);
        while (queue.TryDequeue(out var item))
        {
            item.Callback(item.State);
        }
    }
}