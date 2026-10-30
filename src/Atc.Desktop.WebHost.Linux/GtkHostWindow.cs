namespace Atc.Desktop.WebHost.Linux;

/// <summary>
/// A GTK4 application window with a WebKitGTK webview. The frontend is served from the custom
/// scheme <c>atc-app://app/</c>; messages go page → host through a script message handler and
/// host → page through <c>evaluate_javascript</c>.
/// Modelled on Photino.Native <c>Photino.Linux.cpp</c> (Apache-2.0), rewritten in C#: scheme handler,
/// script message handler, document-start user script, context-menu signal. Differences: uses
/// <c>evaluate_javascript</c> (Photino uses the deprecated <c>run_javascript</c>), marks the scheme
/// secure + CORS-enabled, and doesn't allow every permission request.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class GtkHostWindow : IHostWindow
{
    /// <summary>The custom scheme the frontend is served from.</summary>
    public const string Scheme = "atc-app";

    /// <summary>The page's origin.</summary>
    public const string Origin = Scheme + "://app";

    private const string OriginPrefix = Origin + "/";

    private readonly HostWindowOptions options;
    private readonly IHostWindowEvents events;
    private readonly Gtk.Application application;
    private Gtk.ApplicationWindow? window;
    private WebKit.WebView? webView;
    private bool isClosed;

    private GtkHostWindow(
        HostWindowOptions options,
        IHostWindowEvents events,
        Gtk.Application application)
    {
        this.options = options;
        this.events = events;
        this.application = application;
    }

    public static int Run(
        HostWindowOptions options,
        IHostWindowEvents events)
    {
        Gtk.Module.Initialize();
        WebKit.Module.Initialize();
        events.Mark("gtkInitialized");

        var gtkApplication = Gtk.Application.New(ToApplicationId(options.AppId), Gio.ApplicationFlags.NonUnique);
        var host = new GtkHostWindow(options, events, gtkApplication);
        gtkApplication.OnActivate += (_, _) => host.Activate();
        return gtkApplication.RunWithSynchronizationContext([]);
    }

    public void PostMessage(string json)
    {
        if (webView is null || isClosed)
        {
            return;
        }

        _ = webView
            .EvaluateJavascriptAsync(HostScript.BuildReceiveCall(json))
            .ContinueWith(t => t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    public void SetTitle(string title)
        => window?.SetTitle(title);

    public void BringToFront()
        => window?.Present();

    public void Close()
        => window?.Close();

    public async Task<string?> ShowOpenFileDialogAsync(
        FileDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        using var dialog = CreateDialog(options);
        try
        {
            var file = await dialog.OpenAsync(window!).ConfigureAwait(true);
            return file?.GetPath();
        }
        catch (GLib.GException)
        {
            // Cancelled or dismissed.
            return null;
        }
    }

    public async Task<string?> ShowSaveFileDialogAsync(
        FileDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        using var dialog = CreateDialog(options);
        try
        {
            var file = await dialog.SaveAsync(window!).ConfigureAwait(true);
            return file?.GetPath();
        }
        catch (GLib.GException)
        {
            return null;
        }
    }

    public IReadOnlyList<int> GetBrowserProcessIds()
        => [];

    /// <summary>GApplication ids must look like "org.example.App": letters, digits, '_' and at least one dot.</summary>
    /// <param name="appId">The app id.</param>
    /// <returns>A valid GApplication id.</returns>
    internal static string ToApplicationId(string appId)
    {
        var id = new string([.. appId.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' ? c : '_')]);
        return id.Contains('.', StringComparison.Ordinal) ? id : "net.atc." + id;
    }

    private static Gtk.FileDialog CreateDialog(FileDialogOptions options)
    {
        var dialog = Gtk.FileDialog.New();
        dialog.SetModal(true);
        if (options.Title is not null)
        {
            dialog.SetTitle(options.Title);
        }

        if (options.Filters.Count > 0)
        {
            var filters = Gio.ListStore.New(Gtk.FileFilter.GetGType());
            foreach (var filter in options.Filters)
            {
                var gtkFilter = Gtk.FileFilter.New();
                gtkFilter.SetName(filter.Name);
                foreach (var pattern in filter.Patterns)
                {
                    gtkFilter.AddPattern(pattern);
                }

                filters.Append(gtkFilter);
            }

            dialog.SetFilters(filters);
        }

        return dialog;
    }

    private static void SetBackground(
        WebKit.WebView view,
        HostColor color)
    {
        using var background = new Gdk.RGBA();
        background.Parse(color.ToCss());
        view.SetBackgroundColor(background);
    }

    private void Activate()
    {
        events.Mark("gtkActivated");
        window = Gtk.ApplicationWindow.New(application);
        window.SetTitle(options.Title);
        window.SetDefaultSize(options.Width, options.Height);
        window.OnCloseRequest += (_, _) =>
        {
            isClosed = true;
            events.OnClosed();
            return false;
        };

        RegisterScheme();
        webView = WebKit.WebView.New();
        SetBackground(webView, options.BackgroundColor);
        webView.GetSettings().SetEnableDeveloperExtras(options.DevToolsEnabled);
        webView.OnContextMenu += (_, _) => !options.DevToolsEnabled;

        var contentManager = webView.GetUserContentManager();
        contentManager.RegisterScriptMessageHandler(HostScript.MessageHandlerName, worldName: null);
        contentManager.OnScriptMessageReceived += (_, e) =>
        {
            // Only the app's own page may talk to the host.
            if (IsOnOrigin(webView.GetUri()))
            {
                events.OnMessage(e.Value.ToString());
            }
        };
        webView.OnDecidePolicy += (_, e) => OnDecidePolicy(e.Decision, e.DecisionType);

        window.SetChild(webView);
        window.Present();
        events.OnWindowCreated(this, SynchronizationContext.Current!);
        _ = LoadPageAsync(contentManager);
    }

    private static bool IsOnOrigin(string? uri)
        => uri is not null && uri.StartsWith(OriginPrefix, StringComparison.Ordinal);

    /// <summary>The app's page stays in the window; links to other sites open in the default browser.</summary>
    /// <returns>True when the navigation was handled here (ignored).</returns>
    private static bool OnDecidePolicy(
        WebKit.PolicyDecision decision,
        WebKit.PolicyDecisionType type)
    {
        if (type is not (WebKit.PolicyDecisionType.NavigationAction or WebKit.PolicyDecisionType.NewWindowAction) ||
            decision is not WebKit.NavigationPolicyDecision navigation)
        {
            return false;
        }

        var uri = navigation.GetNavigationAction().GetRequest().GetUri();
        if (type == WebKit.PolicyDecisionType.NavigationAction && IsOnOrigin(uri))
        {
            return false;
        }

        decision.Ignore();
        ExternalLinks.TryOpen(uri);
        return true;
    }

    private async Task LoadPageAsync(WebKit.UserContentManager contentManager)
    {
        var script = await events.CreateDocumentScriptAsync().ConfigureAwait(true);
        contentManager.AddScript(WebKit.UserScript.New(
            script,
            WebKit.UserContentInjectedFrames.TopFrame,
            WebKit.UserScriptInjectionTime.Start,
            allowList: null,
            blockList: null));
        events.Mark("bootScriptAdded");
        webView!.LoadUri(OriginPrefix + options.EntryPath.TrimStart('/'));
        events.Mark("navigationStarted");
    }

    private void RegisterScheme()
    {
        var context = WebKit.WebContext.GetDefault();
        context.RegisterUriScheme(Scheme, OnSchemeRequest);
        var security = context.GetSecurityManager();
        security.RegisterUriSchemeAsSecure(Scheme);
        security.RegisterUriSchemeAsCorsEnabled(Scheme);
    }

    private void OnSchemeRequest(WebKit.URISchemeRequest request)
    {
        var path = request.GetPath().TrimStart('/');
        if (path.Length == 0)
        {
            path = "index.html";
        }

        using var stream = options.Frontend.TryOpen(path);
        byte[] content;
        string contentType;
        if (stream is null)
        {
            content = [];
            contentType = "text/plain";
        }
        else
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            content = memory.ToArray();
            contentType = ContentTypes.FromPath(path);
        }

        using var bytes = GLib.Bytes.New(content);
        using var input = Gio.MemoryInputStream.NewFromBytes(bytes);
        request.Finish(input, content.Length, contentType);
    }
}