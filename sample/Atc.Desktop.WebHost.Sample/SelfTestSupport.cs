namespace Atc.Desktop.WebHost.Sample;

/// <summary>
/// The .NET half of the page's self-test (<c>wwwroot/selftest.js</c>): files to read and watch, and, when started with
/// <c>--selftest</c>, the report and the exit code. The page runs the checks through the real window and bridge.
/// </summary>
public sealed class SelfTestSupport : IDisposable
{
    private readonly WebHostApplication app;
    private readonly string? reportPath;
    private readonly bool isRun;
    private readonly Lock gate = new();
    private string? folder;
    private string? outsideFile;
    private string? wikiFolder;
    private int addedPages;
    private bool reported;

    /// <summary>Initializes a new instance of the <see cref="SelfTestSupport"/> class and adds its requests.</summary>
    /// <param name="app">The app.</param>
    /// <param name="isRun">Started with <c>--selftest</c>: report the results and close the window.</param>
    /// <param name="reportPath">Where the report goes (<c>--selftest-out</c>), or <see langword="null"/>.</param>
    public SelfTestSupport(
        WebHostApplication app,
        bool isRun,
        string? reportPath)
    {
        ArgumentNullException.ThrowIfNull(app);

        this.app = app;
        this.isRun = isRun;
        this.reportPath = reportPath;
        Register();
    }

    /// <summary>Gets a value indicating whether the run failed: a check failed, or the page never reported.</summary>
    public bool Failed { get; private set; } = true;

    /// <summary>Closes the window when the page hasn't reported in time, so a stuck run fails instead of hanging.</summary>
    /// <param name="timeout">How long the page gets.</param>
    public void StartWatchdog(TimeSpan timeout)
        => _ = WatchdogAsync(timeout);

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (folder is not null)
            {
                Directory.Delete(folder, recursive: true);
            }

            if (outsideFile is not null)
            {
                File.Delete(outsideFile);
            }

            if (wikiFolder is not null)
            {
                Directory.Delete(wikiFolder, recursive: true);
            }
        }
        catch (IOException)
        {
            // A temp folder left behind is harmless.
        }
    }

    private async Task WatchdogAsync(TimeSpan timeout)
    {
        try
        {
            await Task.Delay(timeout, app.Lifetime).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        WriteReport([], $"the page reported no results within {timeout.TotalSeconds:0} s");
        app.Post(() => app.Window?.Close());
    }

    private void Register()
    {
        app.Bridge.Handle(
            "selftest.files",
            SampleJsonContext.Default.SelfTestFiles,
            _ => ValueTask.FromResult(CreateFiles()));

        // Changes the watched file, so the page can wait for file.changed.
        app.Bridge.Handle("selftest.touch", async (_, cancellationToken) =>
        {
            var files = CreateFiles();
            await File.AppendAllTextAsync(files.WatchedFile, "touched\n", cancellationToken).ConfigureAwait(false);
            return JsonPayload.Null;
        });

        // Adds a page to the wiki folder's subfolder, so the page can wait for folder.changed. Returns its path.
        app.Bridge.Handle("selftest.addPage", async (_, cancellationToken) =>
        {
            var files = CreateFiles();
            var path = Path.Combine(files.WikiFolder, "Sub", $"New-{Interlocked.Increment(ref addedPages)}.md");
            await File.WriteAllTextAsync(path, "# New\n", cancellationToken).ConfigureAwait(false);
            return JsonPayload.From(path, SampleJsonContext.Default.String);
        });

        app.Bridge.Handle("selftest.done", (request, _) =>
        {
            if (isRun)
            {
                WriteReport(request.Deserialize(SampleJsonContext.Default.SelfTestReport).Results, timeout: null);
                app.Post(() => app.Window?.Close());
            }

            return ValueTask.FromResult(JsonPayload.Null);
        });
    }

    private SelfTestFiles CreateFiles()
    {
        lock (gate)
        {
            // Under the app's own data folder, not the shared temp folder.
            var root = app.Paths.DataFolder;
            var name = $"selftest-{Environment.ProcessId}";
            if (folder is null)
            {
                folder = Path.Combine(root, name);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "watched.txt"), "self-test file\n");
                app.GrantFileAccess(Path.Combine(folder, "watched.txt"));

                outsideFile = Path.Combine(root, $"{name}-outside.txt");
                File.WriteAllText(outsideFile, "outside the allowed folder\n");

                wikiFolder = Path.Combine(root, $"{name}-wiki");
                foreach (var page in (string[])["Home.md", "Sub/Page.MD", "notes.txt", ".git/config.md"])
                {
                    var path = Path.Combine(wikiFolder, page);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, $"# {page}\n");
                }

                app.GrantFolderAccess(wikiFolder);
            }

            var outsideName = Path.GetFileName(outsideFile)!;
            return new SelfTestFiles(
                Path.Combine(folder, "watched.txt"),
                outsideFile!,
                Path.Combine(folder, "..", outsideName),
                wikiFolder!);
        }
    }

    private void WriteReport(
        IReadOnlyList<SelfTestResult> results,
        string? timeout)
    {
        lock (gate)
        {
            if (reported)
            {
                return;
            }

            reported = true;
        }

        var passed = results.Count(r => r.Status == "pass");
        var failed = results.Count(r => r.Status == "fail");
        var skipped = results.Count(r => r.Status == "skip");
        Failed = timeout is not null || failed > 0 || passed == 0;

        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"SELFTEST_PLATFORM={app.Options.Platform.Name}");
        report.AppendLine(CultureInfo.InvariantCulture, $"SELFTEST_PASSED={passed}");
        report.AppendLine(CultureInfo.InvariantCulture, $"SELFTEST_FAILED={failed}");
        report.AppendLine(CultureInfo.InvariantCulture, $"SELFTEST_SKIPPED={skipped}");
        foreach (var result in results)
        {
            var message = string.IsNullOrEmpty(result.Message) ? string.Empty : $" – {result.Message}";
            report.AppendLine(CultureInfo.InvariantCulture, $"{result.Status,-4} {result.Ms,6:0} ms  {result.Name}{message}");
        }

        if (timeout is not null)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"SELFTEST_ERROR={timeout}");
        }

        report.AppendLine(Failed ? "SELFTEST=fail" : "SELFTEST=pass");

        if (reportPath is not null)
        {
            File.WriteAllText(reportPath, report.ToString());
        }
        else
        {
            Console.Write(report.ToString());
        }
    }
}