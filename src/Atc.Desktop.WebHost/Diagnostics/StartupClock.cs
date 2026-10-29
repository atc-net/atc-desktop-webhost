namespace Atc.Desktop.WebHost.Diagnostics;

/// <summary>
/// Startup milestones. Marks are cheap (a <see cref="Stopwatch"/> timestamp); converting them to
/// "milliseconds since the OS started the process" happens only when a report is made.
/// </summary>
public sealed class StartupClock
{
    private readonly ConcurrentQueue<(string Name, long Timestamp)> marks = new();
    private DateTime? processStartUtc;

    /// <summary>Gets the process-wide clock. Call <c>StartupClock.Shared.Mark("main")</c> first thing in <c>Main</c>.</summary>
    public static StartupClock Shared { get; } = new();

    /// <summary>Gets the time the OS started this process, in UTC.</summary>
    public DateTime ProcessStartUtc
    {
        get
        {
            if (processStartUtc is null)
            {
                using var process = Process.GetCurrentProcess();
                processStartUtc = process.StartTime.ToUniversalTime();
            }

            return processStartUtc.Value;
        }
    }

    /// <summary>Records a milestone. Thread-safe; the first mark with a name wins.</summary>
    /// <param name="name">The milestone name.</param>
    public void Mark(string name)
        => marks.Enqueue((name, Stopwatch.GetTimestamp()));

    /// <summary>Gets all milestones in milliseconds since the process started, in the order they happened.</summary>
    /// <returns>Milestone name → milliseconds.</returns>
    public IReadOnlyDictionary<string, double> SinceProcessStart()
    {
        var nowTimestamp = Stopwatch.GetTimestamp();
        var nowMs = (DateTime.UtcNow - ProcessStartUtc).TotalMilliseconds;
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (name, timestamp) in marks)
        {
            result.TryAdd(name, Math.Round(nowMs - Stopwatch.GetElapsedTime(timestamp, nowTimestamp).TotalMilliseconds, 1));
        }

        return result;
    }

    /// <summary>Converts a page timestamp (Unix epoch ms, <c>performance.timeOrigin + performance.now()</c>) to ms since process start.</summary>
    /// <param name="epochMs">The page timestamp.</param>
    /// <returns>Milliseconds since the process started.</returns>
    public double FromEpochMs(double epochMs)
        => Math.Round(epochMs - (ProcessStartUtc - DateTime.UnixEpoch).TotalMilliseconds, 1);
}