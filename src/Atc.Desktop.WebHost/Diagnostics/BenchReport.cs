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
        using var self = Process.GetCurrentProcess();
        var result = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["Host"] = PrivateMb(self),
        };

        var browser = 0.0;
        foreach (var id in browserProcessIds)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                browser += PrivateMb(process);
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

    /// <summary>Sums the <c>Private_Clean</c> and <c>Private_Dirty</c> lines of a Linux <c>/proc/&lt;pid&gt;/smaps_rollup</c> file.</summary>
    /// <param name="smapsRollup">The file content.</param>
    /// <returns>Private memory in kB.</returns>
    public static long ParsePrivateKb(string smapsRollup)
    {
        ArgumentNullException.ThrowIfNull(smapsRollup);

        var total = 0L;
        foreach (var line in smapsRollup.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Private_Clean:", StringComparison.Ordinal) &&
                !trimmed.StartsWith("Private_Dirty:", StringComparison.Ordinal))
            {
                continue;
            }

            var value = trimmed[(trimmed.IndexOf(':', StringComparison.Ordinal) + 1)..]
                .Replace("kB", string.Empty, StringComparison.Ordinal)
                .Trim();
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
            {
                total += kb;
            }
        }

        return total;
    }

    /// <summary>
    /// Private memory in MB. On Linux, <see cref="Process.PrivateMemorySize64"/> is reserved address space
    /// (tens of GB for a .NET process), so the pages in use are read from <c>/proc</c> instead.
    /// </summary>
    private static double PrivateMb(Process process)
    {
        const double Mb = 1024.0 * 1024.0;
        if (OperatingSystem.IsLinux())
        {
            try
            {
                return ParsePrivateKb(File.ReadAllText($"/proc/{process.Id}/smaps_rollup")) / 1024.0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Older kernels have no smaps_rollup; fall back to the resident set.
                return process.WorkingSet64 / Mb;
            }
        }

        return process.PrivateMemorySize64 / Mb;
    }

    private static void Append(
        StringBuilder sb,
        string key,
        double value)
        => sb.Append(key).Append('=').AppendLine(value.ToString("F1", CultureInfo.InvariantCulture));
}