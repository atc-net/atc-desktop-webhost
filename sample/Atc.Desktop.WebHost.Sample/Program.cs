namespace Atc.Desktop.WebHost.Sample;

/// <summary>
/// Demo and benchmark app for Atc.Desktop.WebHost: opens a text file and shows it, with startup
/// timings, the bridge, theme, file watching and single instance.
/// <code>
/// atc-webhost-sample [file] [--ui=folder|embedded] [--devtools] [--new-instance]
/// atc-webhost-sample file --bench --bench-out result.txt
/// </code>
/// </summary>
public static class Program
{
    /// <summary>Starts the app. <see cref="STAThreadAttribute"/> lets WebView2 run on the main thread.</summary>
    /// <param name="args">File paths and switches.</param>
    /// <returns>The exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        StartupClock.Shared.Mark("main");
        var commandLine = WebHostCommandLine.Parse(args);

        using var app = new WebHostApplication(new WebHostOptions
        {
            AppId = "Atc.Desktop.WebHost.Sample",
            Title = "Atc.Desktop.WebHost sample",
            Platform = CreatePlatform(),
            Frontend = CreateFrontend(commandLine),
            LoadDocument = TextDocumentLoader.LoadAsync,
            OpenFileFilters = [new FileDialogFilter("Text", ["*.md", "*.txt", "*.json", "*.cs", "*.log"])],
            AdditionalBrowserArguments = commandLine.GetSwitch("browser-args"),
        });

        SampleHandlers.Register(app);
        return app.Run(commandLine);
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