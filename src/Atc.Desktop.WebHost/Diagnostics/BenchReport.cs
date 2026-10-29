namespace Atc.Desktop.WebHost.Diagnostics;

/// <summary>
/// Writes a <c>--bench-out</c> file: one <c>KEY=VALUE</c> per line, the format of Per's
/// <c>benchmarks/measure.ps1</c>, so numbers from both repos can be compared.
/// </summary>
internal static class BenchReport
{
    public const string StartupMetric = "STARTUP_MS";

    public static string Format(
        IReadOnlyDictionary<string, double> hostMarks,
        IReadOnlyDictionary<string, double> pageMarks,
        IReadOnlyDictionary<string, double> memoryMb,
        string platform)
    {
        var sb = new StringBuilder();
        if (hostMarks.TryGetValue("firstPaint", out var firstPaint))
        {
            Append(sb, StartupMetric, firstPaint);
        }

        if (memoryMb.Count > 0)
        {
            Append(sb, "PRIVATE_MB", memoryMb.Values.Sum());
            foreach (var (kind, mb) in memoryMb)
            {
                Append(sb, "MEM_" + kind + "_MB", mb);
            }
        }

        foreach (var (name, ms) in hostMarks)
        {
            Append(sb, "MARK_" + name, ms);
        }

        foreach (var (name, ms) in pageMarks)
        {
            Append(sb, "PAGE_" + name, ms);
        }

        sb.Append("PLATFORM=").AppendLine(platform);
        sb.Append("RUNTIME=").AppendLine(Environment.Version.ToString());
        return sb.ToString();
    }

    /// <summary>Private memory in MB: this process, then the webview processes summed.</summary>
    /// <param name="browserProcessIds">The webview's process ids.</param>
    /// <returns>Kind → MB.</returns>
    public static IReadOnlyDictionary<string, double> MeasureMemory(
        IReadOnlyList<int> browserProcessIds)
    {
        const double Mb = 1024.0 * 1024.0;
        using var self = Process.GetCurrentProcess();
        var result = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["Host"] = self.PrivateMemorySize64 / Mb,
        };

        var browser = 0.0;
        foreach (var id in browserProcessIds)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                browser += process.PrivateMemorySize64 / Mb;
            }
            catch (ArgumentException)
            {
                // The process exited between listing and reading.
            }
        }

        if (browserProcessIds.Count > 0)
        {
            result["WebView"] = browser;
        }

        return result;
    }

    private static void Append(
        StringBuilder sb,
        string key,
        double value)
        => sb.Append(key).Append('=').AppendLine(value.ToString("F1", CultureInfo.InvariantCulture));
}