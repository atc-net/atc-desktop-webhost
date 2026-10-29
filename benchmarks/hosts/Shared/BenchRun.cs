using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace AtcBench;

/// <summary>
/// The part every comparison host shares: command line, startup marks, document loading, the page shim and the
/// <c>--bench-out</c> report. It follows the same rules as Atc.Desktop.WebHost so the numbers compare like for like
/// (see benchmarks/hosts/README.md).
/// </summary>
public static class BenchRun
{
    /// <summary>Same private origin as the host.</summary>
    public const string HostName = "app.atc-webhost.example";

    private const string Shim = """
        (() => {
          const boot = __BOOT__;
          const post = (message) => window.chrome.webview.postMessage(JSON.stringify(message));
          window.atcHost = Object.freeze({
            platform: boot.platform,
            boot,
            request: () => Promise.resolve({}),
            notify: (type, payload) => post({ kind: "request", type, payload }),
            on: () => () => {},
            ready: () => {},
          });
        })();
        """;

    private static readonly ConcurrentQueue<(string Name, long Timestamp)> Marks = new();
    private static Task<string> documentJson = Task.FromResult("null");
    private static string platform = "unknown";
    private static Dictionary<string, double> pageMarks = new(StringComparer.Ordinal);

    public static string? FilePath { get; private set; }

    public static bool IsBench { get; private set; }

    public static string? BenchOut { get; private set; }

    public static string StartUrl => $"https://{HostName}/index.html";

    /// <summary>Call first thing in the app: parses the command line and starts reading the document on the thread pool.</summary>
    /// <param name="hostPlatform">Shown in the page footer and written as PLATFORM.</param>
    public static void Start(string hostPlatform)
    {
        Mark("main");
        platform = hostPlatform;
        var args = Environment.GetCommandLineArgs();
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--bench":
                    IsBench = true;
                    break;
                case "--bench-out" when i + 1 < args.Length:
                    BenchOut = args[++i];
                    break;
                default:
                    if (!args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        FilePath ??= Path.GetFullPath(args[i]);
                    }

                    break;
            }
        }

        if (FilePath is { } path)
        {
            documentJson = Task.Run(() => LoadAsync(path));
        }
    }

    /// <summary>Records a milestone.</summary>
    public static void Mark(string name)
        => Marks.Enqueue((name, Stopwatch.GetTimestamp()));

    /// <summary>A persistent WebView2 profile per host, so warm launches are warm.</summary>
    public static string DataFolder(string host)
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtcBench",
            host,
            "WebView2");

    /// <summary>
    /// Configures the webview the way the host does, injects the boot script and navigates.
    /// The boot script is awaited before navigating, as the WebView2 documentation describes.
    /// </summary>
    public static async Task ShowPageAsync(
        CoreWebView2 view,
        Action close)
    {
        var settings = view.Settings;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsReputationCheckingRequired = false;

        view.WebMessageReceived += (_, e) =>
        {
            if (OnMessage(e.TryGetWebMessageAsString(), view))
            {
                close();
            }
        };
        view.SetVirtualHostNameToFolderMapping(
            HostName,
            Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            CoreWebView2HostResourceAccessKind.Allow);
        Mark("webviewConfigured");

        var document = await documentJson;
        var boot = "{\"appId\":\"AtcBench\",\"platform\":\"" + platform + "\",\"theme\":\"light\",\"origin\":\"https://" + HostName +
                   "\",\"bench\":" + (IsBench ? "true" : "false") + ",\"files\":[],\"settings\":{},\"document\":" + document +
                   ",\"documentPending\":false}";
        await view.AddScriptToExecuteOnDocumentCreatedAsync(Shim.Replace("__BOOT__", boot, StringComparison.Ordinal));
        Mark("bootScriptAdded");
        view.Navigate(StartUrl);
        Mark("navigationStarted");
    }

    /// <returns>True when the app should close (bench mode, first paint reported).</returns>
    private static bool OnMessage(
        string message,
        CoreWebView2 view)
    {
        using var json = JsonDocument.Parse(message);
        if (!json.RootElement.TryGetProperty("type", out var type) || type.GetString() != "host.firstPaint")
        {
            return false;
        }

        Mark("firstPaint");
        if (json.RootElement.TryGetProperty("payload", out var payload) &&
            payload.TryGetProperty("marks", out var marks))
        {
            pageMarks = marks.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Number)
                .ToDictionary(p => p.Name, p => FromEpochMs(p.Value.GetDouble()), StringComparer.Ordinal);
        }

        if (!IsBench)
        {
            return false;
        }

        if (BenchOut is { } file)
        {
            File.WriteAllText(file, FormatReport(view));
        }

        return true;
    }

    private static async Task<string> LoadAsync(string path)
    {
        var started = Stopwatch.GetTimestamp();
        var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var lines = text.AsSpan().Count('\n') + (text.Length > 0 && text[^1] != '\n' ? 1 : 0);
        var document = new BenchDocument(
            path,
            Path.GetFileName(path),
            text,
            lines,
            new FileInfo(path).Length,
            Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2));
        return JsonSerializer.Serialize(document, BenchJsonContext.Default.BenchDocument);
    }

    private static string FormatReport(CoreWebView2 view)
    {
        var hostMarks = SinceProcessStart();
        var sb = new StringBuilder();
        Append(sb, "STARTUP_MS", hostMarks["firstPaint"]);

        using var self = Process.GetCurrentProcess();
        var hostMb = self.PrivateMemorySize64 / (1024.0 * 1024.0);
        var webViewMb = 0.0;
        foreach (var info in view.Environment.GetProcessInfos())
        {
            try
            {
                using var process = Process.GetProcessById(info.ProcessId);
                webViewMb += process.PrivateMemorySize64 / (1024.0 * 1024.0);
            }
            catch (ArgumentException)
            {
                // The process exited between listing and reading.
            }
        }

        Append(sb, "PRIVATE_MB", hostMb + webViewMb);
        Append(sb, "MEM_Host_MB", hostMb);
        Append(sb, "MEM_WebView_MB", webViewMb);
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

    /// <summary>Milliseconds since the OS started the process, computed like the host's StartupClock.</summary>
    private static Dictionary<string, double> SinceProcessStart()
    {
        var nowTimestamp = Stopwatch.GetTimestamp();
        var nowMs = (DateTime.UtcNow - ProcessStartUtc()).TotalMilliseconds;
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (name, timestamp) in Marks)
        {
            result.TryAdd(name, Math.Round(nowMs - Stopwatch.GetElapsedTime(timestamp, nowTimestamp).TotalMilliseconds, 1));
        }

        return result;
    }

    private static double FromEpochMs(double epochMs)
        => Math.Round(epochMs - (ProcessStartUtc() - DateTime.UnixEpoch).TotalMilliseconds, 1);

    private static DateTime ProcessStartUtc()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime();
    }

    private static void Append(
        StringBuilder sb,
        string key,
        double value)
        => sb.Append(key).Append('=').AppendLine(value.ToString("F1", CultureInfo.InvariantCulture));
}
