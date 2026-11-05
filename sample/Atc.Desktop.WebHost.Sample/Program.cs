namespace Atc.Desktop.WebHost.Sample;

/// <summary>
/// Demo, benchmark and self-test app for Atc.Desktop.WebHost: a tour of the host's features around a text viewer.
/// <code>
/// atc-webhost-sample [file] [--ui=folder|embedded] [--devtools] [--new-instance]
/// atc-webhost-sample file --bench --bench-out result.txt
/// atc-webhost-sample [file] --selftest [--selftest-out report.txt]
/// atc-webhost-sample [file] --isolated
/// </code>
/// <c>--selftest</c> runs the page's self-test, writes a report and exits with 1 when a check failed.
/// <c>--isolated</c> (implied by <c>--selftest</c>) uses its own settings and no single instance, for test drivers.
/// </summary>
public static class Program
{
    private const string AppId = "Atc.Desktop.WebHost.Sample";

    /// <summary>Starts the app. <see cref="STAThreadAttribute"/> lets WebView2 run on the main thread.</summary>
    /// <param name="args">File paths and switches.</param>
    /// <returns>The exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        StartupClock.Shared.Mark("main");
        var commandLine = WebHostCommandLine.Parse(args);
        var isSelfTestRun = commandLine.HasSwitch("selftest");
        var isIsolated = isSelfTestRun || commandLine.HasSwitch("isolated");

        using var app = new WebHostApplication(new WebHostOptions
        {
            AppId = isIsolated ? $"{AppId}.Test" : AppId,
            Title = "Atc.Desktop.WebHost sample",
            Platform = CreatePlatform(),
            Frontend = CreateFrontend(commandLine),
            LoadDocument = TextDocumentLoader.LoadAsync,
            OpenFileFilters = [new FileDialogFilter("Text", ["*.md", "*.txt", "*.json", "*.cs", "*.log"])],
            AdditionalBrowserArguments = commandLine.GetSwitch("browser-args"),
            SingleInstance = !isIsolated,
            Languages = ["en", "da-DK", "de-DE"],

            // Extra files on their own origin: the tour shows an image, a fetch and an iframe from here.
            Mounts = [new FrontendMount("tour", FrontendSource.FromFolder(Path.Combine(AppContext.BaseDirectory, "tour-files")))],
        });

        SampleHandlers.Register(app, isSelfTestRun);
        using var selfTest = new SelfTestSupport(app, isSelfTestRun, commandLine.GetSwitch("selftest-out"));
        if (isSelfTestRun)
        {
            selfTest.StartWatchdog(TimeSpan.FromSeconds(90));
        }

        var exitCode = app.Run(commandLine);
        return isSelfTestRun && selfTest.Failed ? 1 : exitCode;
    }

    private static IHostPlatform CreatePlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WebView2Platform();
        }

        if (OperatingSystem.IsLinux())
        {
            return new WebKitGtkPlatform();
        }

        throw new PlatformNotSupportedException("The sample runs on Windows and Linux.");
    }

    private static FrontendSource CreateFrontend(WebHostCommandLine commandLine)
        => string.Equals(commandLine.GetSwitch("ui"), "embedded", StringComparison.OrdinalIgnoreCase)
            ? FrontendSource.FromEmbeddedResources(typeof(Program).Assembly, "wwwroot/")
            : FrontendSource.FromFolder(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
}