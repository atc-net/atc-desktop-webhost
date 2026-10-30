namespace Atc.Desktop.WebHost;

/// <summary>
/// A desktop app: a native window with a webview showing a web frontend, plus the bridge between them.
/// <para>
/// Startup pipeline (cold start), optimized for time to first content:
/// <list type="number">
/// <item>Single instance check (a mutex; a second launch hands its files over and exits).</item>
/// <item>The first document starts loading on the thread pool, in parallel with everything below.</item>
/// <item>The native window appears at once, painted in the theme's background colour.</item>
/// <item>The webview starts (the slow part: ~80% of startup on WebView2).</item>
/// <item>The boot script is injected: <c>window.atcHost</c>, settings, theme and the document if it's ready.</item>
/// <item>The page loads from a private origin and paints the document without a round trip.</item>
/// </list>
/// </para>
/// </summary>
public sealed partial class WebHostApplication : IHostWindowEvents, IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly FileWatchService fileWatch;
    private readonly PathScope pathScope;
    private WebHostCommandLine commandLine = WebHostCommandLine.Parse([]);
    private Task<JsonPayload>? initialDocument;
    private SingleInstance? singleInstance;
    private SynchronizationContext? uiContext;
    private int uiThreadId;
    private string theme = "light";
    private IReadOnlyDictionary<string, double> pageMarks = new Dictionary<string, double>(StringComparer.Ordinal);
    private int disposed;

    /// <summary>Initializes a new instance of the <see cref="WebHostApplication"/> class.</summary>
    /// <param name="options">The app settings.</param>
    public WebHostApplication(WebHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Clock.Mark("hostCreated");
        Options = options;
        Paths = HostPaths.Resolve(options.Vendor, options.AppId);
        Settings = new SettingsStore(Paths.ConfigFolder);
        fileWatch = new FileWatchService(OnFileChanged, options.FileChangeDebounce);
        pathScope = new PathScope(options.RestrictFileAccess);
        RegisterBuiltInHandlers();
    }

    /// <summary>Gets the app settings.</summary>
    public WebHostOptions Options { get; }

    /// <summary>Gets the bridge: add request handlers and send events here.</summary>
    public WebBridge Bridge { get; } = new();

    /// <summary>Gets the persisted user settings.</summary>
    public SettingsStore Settings { get; }

    /// <summary>Gets the app's folders.</summary>
    public HostPaths Paths { get; }

    /// <summary>Gets the startup clock.</summary>
    public static StartupClock Clock
        => StartupClock.Shared;

    /// <summary>Gets the native window, once created.</summary>
    public IHostWindow? Window { get; private set; }

    /// <summary>Gets the parsed command line, once <see cref="Run(string[])"/> is called.</summary>
    public WebHostCommandLine CommandLine
        => commandLine;

    /// <summary>Gets a token that is cancelled when the window closes.</summary>
    public CancellationToken Lifetime
        => lifetime.Token;

    /// <summary>
    /// Lets the page read and watch this file's folder and everything below it. The host already does this for
    /// files from the command line, a second launch and the Open and Save dialogs; call it when the app gets a
    /// file another way (for example its own recent-files list).
    /// </summary>
    /// <param name="filePath">A file the user chose.</param>
    public void GrantFileAccess(string filePath)
        => pathScope.Grant(filePath);

    /// <summary>Runs the app until the window closes.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public int Run(string[] args)
        => Run(WebHostCommandLine.Parse(args));

    /// <summary>Runs the app until the window closes.</summary>
    /// <param name="parsedCommandLine">The parsed command line.</param>
    /// <returns>The process exit code.</returns>
    public int Run(WebHostCommandLine parsedCommandLine)
    {
        ArgumentNullException.ThrowIfNull(parsedCommandLine);

        commandLine = parsedCommandLine;
        var platform = Options.Platform;
        if (!platform.IsSupported)
        {
            throw new PlatformNotSupportedException($"The '{platform.Name}' window doesn't run on {Environment.OSVersion}.");
        }

        Clock.Mark("run");
        var files = commandLine.Files;
        foreach (var file in files)
        {
            pathScope.Grant(file);
        }

        if (Options.SingleInstance && !commandLine.IsBench && !commandLine.IsNewInstance)
        {
            singleInstance = SingleInstance.Acquire(Options.AppId, files, platform.AllowActivationHandOff);
            Clock.Mark("singleInstanceChecked");
            if (!singleInstance.IsPrimary)
            {
                return 0;
            }
        }

        if (files.Count > 0 && Options.LoadDocument is not null)
        {
            var first = files[0];
            initialDocument = Task.Run(() => LoadDocumentAsync(first, isInitial: true), Lifetime);
        }

        theme = ResolveTheme(platform);
        var windowOptions = new HostWindowOptions
        {
            AppId = Options.AppId,
            Title = Options.Title,
            Width = Options.Width,
            Height = Options.Height,
            IsDark = theme == "dark",
            BackgroundColor = theme == "dark" ? Options.DarkBackground : Options.LightBackground,
            Frontend = Options.Frontend,
            EntryPath = Options.EntryPath,
            DataFolder = Paths.WebViewFolder,
            DevToolsEnabled = Options.DevToolsEnabled || commandLine.IsDevTools,
            AdditionalBrowserArguments = Options.AdditionalBrowserArguments,
            IconPath = Options.IconPath,
        };

        Clock.Mark("platformRun");
        return platform.Run(windowOptions, this);
    }

    /// <summary>Runs an action on the UI thread (directly when already there).</summary>
    /// <param name="action">The action.</param>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (uiContext is null || Environment.CurrentManagedThreadId == uiThreadId)
        {
            action();
            return;
        }

        uiContext.Post(static state => ((Action)state!)(), action);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        lifetime.Cancel();
        fileWatch.Dispose();
        singleInstance?.Dispose();
        lifetime.Dispose();
    }

    /// <inheritdoc />
    void IHostWindowEvents.OnWindowCreated(
        IHostWindow window,
        SynchronizationContext uiContext)
    {
        Clock.Mark("windowCreated");
        Window = window;
        this.uiContext = uiContext;
        uiThreadId = Environment.CurrentManagedThreadId;
        Bridge.Connect(message => Post(() => window.PostMessage(message)));
        singleInstance?.Listen(OnFilesFromSecondInstance);

        if (commandLine.IsBench)
        {
            // A page that never paints must not hang the benchmark run.
            _ = CloseAfterAsync(TimeSpan.FromSeconds(60));
        }
    }

    /// <inheritdoc />
    void IHostWindowEvents.Mark(string name)
        => Clock.Mark(name);

    /// <inheritdoc />
    async ValueTask<string> IHostWindowEvents.CreateDocumentScriptAsync()
    {
        var document = initialDocument;
        if (document is not null && !document.IsCompleted)
        {
            await Task.WhenAny(document, Task.Delay(Options.InlineDocumentWait, Lifetime)).ConfigureAwait(true);
        }

        var inline = document is { IsCompletedSuccessfully: true };
        var inlineDocument = inline ? await document!.ConfigureAwait(true) : null;
        var script = HostScript.Build(BuildBootJson(inlineDocument, documentPending: document is not null && !inline));
        Clock.Mark(inline ? "bootScriptWithDocument" : "bootScriptWithoutDocument");
        return script;
    }

    /// <inheritdoc />
    void IHostWindowEvents.OnMessage(string message)
        => _ = Bridge.DispatchAsync(message);

    /// <inheritdoc />
    void IHostWindowEvents.OnClosed()
    {
        Clock.Mark("closed");
        Dispose();
    }

    private static string ResolveThemeSetting(string? setting) => setting switch
    {
        "dark" => "dark",
        "light" => "light",
        _ => "system",
    };

    private string ResolveTheme(IHostPlatform platform)
        => ResolveThemeSetting(Settings.GetString("theme")) switch
        {
            "system" => platform.IsSystemDarkTheme() ? "dark" : "light",
            var explicitTheme => explicitTheme,
        };

    private string BuildBootJson(
        JsonPayload? document,
        bool documentPending)
    {
        var buffer = new ArrayBufferWriter<byte>(document is null ? 1024 : (document.Json.Length * 2) + 1024);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("appId", Options.AppId);
            writer.WriteString("platform", Options.Platform.Name);
            writer.WriteString("theme", theme);
            writer.WriteString("origin", FrontendSource.Origin);
            writer.WriteBoolean("bench", commandLine.IsBench);
            writer.WriteStartArray("files");
            foreach (var file in commandLine.Files)
            {
                writer.WriteStringValue(file);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("settings");
            Settings.Get().WriteTo(writer);
            writer.WriteBoolean("documentPending", documentPending);
            writer.WritePropertyName("document");
            writer.WriteRawValue(document?.Json ?? "null", skipInputValidation: true);
            writer.WritePropertyName("hostMarks");
            JsonSerializer.Serialize(writer, Clock.SinceProcessStart(), BridgeJsonContext.Default.IReadOnlyDictionaryStringDouble);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private async Task<JsonPayload> LoadDocumentAsync(
        string path,
        bool isInitial)
    {
        if (isInitial)
        {
            Clock.Mark("documentLoadStarted");
        }

        try
        {
            var loader = Options.LoadDocument
                ?? throw new BridgeException("not_supported", "This app doesn't load documents.");
            return await loader(path, Lifetime).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return JsonPayload.From(new BuiltInMessages.DocumentError(path, ex.Message), BridgeJsonContext.Default.DocumentError);
        }
        finally
        {
            if (isInitial)
            {
                Clock.Mark("documentLoaded");
            }
        }
    }

    private void OnFilesFromSecondInstance(IReadOnlyList<string> files)
        => Post(() =>
        {
            Window?.BringToFront();
            foreach (var file in files)
            {
                pathScope.Grant(file);
                _ = OpenFromSecondInstanceAsync(file);
            }
        });

    private async Task OpenFromSecondInstanceAsync(string file)
    {
        var document = await Task.Run(() => LoadDocumentAsync(file, isInitial: false), Lifetime).ConfigureAwait(true);
        Bridge.Emit("document.opened", document);
    }

    private void OnFileChanged(
        string path,
        string kind)
        => Bridge.Emit(
            "file.changed",
            new BuiltInMessages.FileChangedEvent(path, kind),
            BridgeJsonContext.Default.FileChangedEvent);

    private async Task CloseAfterAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, Lifetime).ConfigureAwait(true);
            Window?.Close();
        }
        catch (OperationCanceledException)
        {
            // Closed normally.
        }
    }
}