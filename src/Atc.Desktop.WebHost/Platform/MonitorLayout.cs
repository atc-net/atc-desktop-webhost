namespace Atc.Desktop.WebHost.Platform;

/// <summary>Numbers displays and finds the one a <see cref="WindowPlacement"/> asks for. Shared by the platforms.</summary>
internal static class MonitorLayout
{
    /// <summary>Numbers displays left to right, then top to bottom, starting at 1.</summary>
    /// <param name="monitors">The displays in any order; their <see cref="HostMonitor.Number"/> is ignored.</param>
    /// <returns>The displays in order, numbered.</returns>
    public static IReadOnlyList<HostMonitor> Number(
        IEnumerable<HostMonitor> monitors)
        => monitors
            .OrderBy(m => m.Bounds.X)
            .ThenBy(m => m.Bounds.Y)
            .Select((m, i) => m with { Number = i + 1 })
            .ToList();

    /// <summary>Finds a display by number, falling back to the primary display (or the first one).</summary>
    /// <param name="monitors">The numbered displays.</param>
    /// <param name="number">The wanted number, or <see langword="null"/>.</param>
    /// <returns>The display, or <see langword="null"/> when there are none.</returns>
    public static HostMonitor? Find(
        IReadOnlyList<HostMonitor> monitors,
        int? number)
        => monitors.FirstOrDefault(m => m.Number == number)
           ?? monitors.FirstOrDefault(m => m.IsPrimary)
           ?? (monitors.Count > 0 ? monitors[0] : null);
}