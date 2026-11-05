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

    /// <summary>The origin mounts are served from.</summary>
    public const string FilesOrigin = Scheme + "://files";

    private const string OriginPrefix = Origin + "/";

    private readonly HostWindowOptions options;
    private readonly IHostWindowEvents events;
    private readonly Gtk.Application application;
    private Gtk.ApplicationWindow? window;
    private WebKit.WebView? webView;
    private MountTable? mounts;
    private bool isClosed;
    private bool closeFromCode;
    private WindowPlacement activePlacement;

    private GtkHostWindow(
        HostWindowOptions options,
        IHostWindowEvents events,
        Gtk.Application application)
    {
        this.options = options;
        this.events = events;
        this.application = application;
        activePlacement = options.Placement;
    }

    public WindowPlacement Placement
        => activePlacement;

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
    {
        // Closing from code skips the close guard; only the user's close asks the page.
        closeFromCode = true;
        window?.Close();
    }

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

    public async Task<string?> ShowOpenFolderDialogAsync(
        FileDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        using var dialog = CreateDialog(new FileDialogOptions { Title = options.Title });
        try
        {
            var folder = await dialog.SelectFolderAsync(window!).ConfigureAwait(true);
            return folder?.GetPath();
        }
        catch (GLib.GException)
        {
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
        => ProcessTree.GetDescendants(Environment.ProcessId);

    /// <summary>
    /// GTK 4 can go fullscreen on a chosen display. It can't position a normal window (Wayland doesn't allow it)
    /// or hide it from the taskbar, so for a normal window the display number and the taskbar flag are ignored.
    /// </summary>
    /// <param name="placement">Where the window goes.</param>
    public void SetPlacement(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (window is null)
        {
            return;
        }

        if (!placement.Fullscreen)
        {
            window.Unfullscreen();
        }
        else if (GtkMonitors.Find(placement.Monitor) is { } monitor)
        {
            window.FullscreenOnMonitor(monitor);
        }
        else
        {
            window.Fullscreen();
        }

        activePlacement = placement;
        events.OnPlacementChanged(placement);
    }

    /// <summary>WebKitGTK has no find bar; the page keeps Ctrl+F on Linux.</summary>
    /// <param name="text">Ignored.</param>
    public void Find(string? text)
    {
    }

    public void Print()
    {
        if (webView is null || window is null)
        {
            return;
        }

        using var operation = WebKit.PrintOperation.New(webView);
        operation.RunDialog(window);
    }

    /// <summary>Trims the right-click menu to <see cref="HostWindowOptions.ContextMenu"/>.</summary>
    /// <returns><see langword="true"/> to show the (trimmed) menu.</returns>
    private bool ShowContextMenu(WebKit.ContextMenu menu)
    {
        var mode = options.DevToolsEnabled ? HostContextMenu.Full : options.ContextMenu;
        if (mode != HostContextMenu.Edit)
        {
            return mode == HostContextMenu.Full;
        }

        for (var i = (int)menu.GetNItems() - 1; i >= 0; i--)
        {
            var item = menu.GetItemAtPosition((uint)i);
            if (item is null)
            {
                continue;
            }

            var keep = !item.IsSeparator() && item.GetStockAction() is
                WebKit.ContextMenuAction.Copy or
                WebKit.ContextMenuAction.Cut or
                WebKit.ContextMenuAction.Paste or
                WebKit.ContextMenuAction.PasteAsPlainText or
                WebKit.ContextMenuAction.SelectAll or
                WebKit.ContextMenuAction.CopyLinkToClipboard or
                WebKit.ContextMenuAction.CopyImageToClipboard;
            if (!keep)
            {
                menu.Remove(item);
            }
        }

        return menu.GetNItems() > 0;
    }

    /// <summary>Standard keys (<see cref="HostKeys"/>) before the page sees them: F11, Ctrl+P, Ctrl+F4.</summary>
    /// <returns><see langword="true"/> when the key was handled here.</returns>
    private bool OnKeyPressed(
        uint key,
        Gdk.ModifierType state)
    {
        const uint keyF4 = 0xFFC1;
        const uint keyF11 = 0xFFC8;
        const uint keyLowerP = 0x70;
        const uint keyUpperP = 0x50;

        var ctrl = state.HasFlag(Gdk.ModifierType.ControlMask);
        var keys = options.Keys;
        if (key == keyF11 && !ctrl && keys.HasFlag(HostKeys.Fullscreen) && window is not null)
        {
            // GTK puts the window back to its earlier size itself.
            SetPlacement(new WindowPlacement
            {
                Monitor = activePlacement.Monitor,
                Fullscreen = !window.IsFullscreen(),
                ShowInTaskbar = activePlacement.ShowInTaskbar,
            });
            return true;
        }

        if (ctrl && key is keyLowerP or keyUpperP && keys.HasFlag(HostKeys.Print))
        {
            Print();
            return true;
        }

        if (ctrl && key == keyF4 && keys.HasFlag(HostKeys.Close))
        {
            // Like the title-bar button: the close guard is asked.
            window?.Close();
            return true;
        }

        return false;
    }

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
        var keyController = Gtk.EventControllerKey.New();
        keyController.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        keyController.OnKeyPressed += (_, e) => OnKeyPressed(e.Keyval, e.State);
        window.AddController(keyController);
        if (!options.Placement.IsDefault)
        {
            SetPlacement(options.Placement);
        }

        window.OnCloseRequest += (_, _) =>
        {
            // Returning true keeps the window open.
            if (!closeFromCode && !events.OnCloseRequested())
            {
                return true;
            }

            isClosed = true;
            events.OnClosed();
            return false;
        };

        // Window decorations, dialogs and the page's prefers-color-scheme follow the app's theme.
        Gtk.Settings.GetDefault()?.GtkApplicationPreferDarkTheme = options.IsDark;

        RegisterScheme();
        webView = CreateWebView(options.DataFolder);
        SetBackground(webView, options.BackgroundColor);
        webView.GetSettings().SetEnableDeveloperExtras(options.DevToolsEnabled);
        webView.OnContextMenu += (_, e) => !ShowContextMenu(e.ContextMenu);

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
        webView.OnLoadChanged += (_, e) =>
        {
            if (e.LoadEvent == WebKit.LoadEvent.Started)
            {
                events.OnPageUnloaded();
            }
        };
        webView.OnWebProcessTerminated += (_, _) => events.OnPageUnloaded();

        window.SetChild(webView);
        window.Present();
        events.OnWindowCreated(this, SynchronizationContext.Current!);
        _ = LoadPageAsync(contentManager);
    }

    /// <summary>
    /// Creates the webview with its own profile (local storage, cookies, cache) in <paramref name="dataFolder"/>,
    /// like the WebView2 user data folder on Windows. The default session would share WebKit's folders per user.
    /// </summary>
    private static WebKit.WebView CreateWebView(string dataFolder)
    {
        var session = WebKit.NetworkSession.New(dataFolder, Path.Combine(dataFolder, "cache"));
#pragma warning disable CA2000 // Disposing the argument also frees the value.
        var value = new GObject.Value();
#pragma warning restore CA2000
        value.Init(WebKit.NetworkSession.GetGType());
        value.SetObject(session);

        using var argument = new GObject.ConstructArgument("network-session", value);
        return WebKit.WebView.NewWithProperties([argument]);
    }

    private static bool IsOnOrigin(string? uri)
        => uri is not null && uri.StartsWith(OriginPrefix, StringComparison.Ordinal);

    private static bool IsOnFilesOrigin(string? uri)
        => uri is not null &&
           uri.Length > FilesOrigin.Length &&
           uri.StartsWith(FilesOrigin, StringComparison.Ordinal) &&
           uri[FilesOrigin.Length] == '/';

    /// <summary>
    /// The app's page stays in the window; links to other sites open in the default browser. Iframes may show web
    /// pages (e.g. a video embed). WebKit asks about iframe navigations too and can't say which frame navigates,
    /// so web addresses pass the navigation step and the main frame is checked when the response arrives.
    /// </summary>
    /// <returns>True when the navigation was handled here (ignored).</returns>
    private static bool OnDecidePolicy(
        WebKit.PolicyDecision decision,
        WebKit.PolicyDecisionType type)
    {
        switch (type)
        {
            case WebKit.PolicyDecisionType.NewWindowAction when decision is WebKit.NavigationPolicyDecision newWindow:
            {
                var uri = newWindow.GetNavigationAction().GetRequest().GetUri();
                decision.Ignore();
                if (!IsOnFilesOrigin(uri))
                {
                    ExternalLinks.TryOpen(uri);
                }

                return true;
            }

            case WebKit.PolicyDecisionType.NavigationAction when decision is WebKit.NavigationPolicyDecision navigation:
            {
                var action = navigation.GetNavigationAction();
                var uri = action.GetRequest().GetUri();
                if (action.GetNavigationType() == WebKit.NavigationType.LinkClicked && !IsOnOrigin(uri) && !IsOnFilesOrigin(uri))
                {
                    // A clicked link to another site (in the app's page or in an embed) opens in the browser.
                    decision.Ignore();
                    ExternalLinks.TryOpen(uri);
                    return true;
                }

                if (FramePolicy.MayLoadInFrame(uri, Origin, FilesOrigin))
                {
                    return false;
                }

                // Other schemes (mailto:, file:, …): never inside the app.
                decision.Ignore();
                ExternalLinks.TryOpen(uri);
                return true;
            }

            case WebKit.PolicyDecisionType.Response when decision is WebKit.ResponsePolicyDecision response &&
                                                          response.IsMainFrameMainResource():
            {
                var uri = response.GetRequest().GetUri();
                if (IsOnOrigin(uri))
                {
                    return false;
                }

                // The main frame is leaving the app: show it in the browser instead. Messages only come from the app's page.
                decision.Ignore();
                if (!IsOnFilesOrigin(uri))
                {
                    ExternalLinks.TryOpen(uri);
                }

                return true;
            }

            default:
                return false;
        }
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

    /// <summary>Serves the app's frontend (<c>atc-app://app/</c>) and the mounts (<c>atc-app://files/</c>).</summary>
    private void OnSchemeRequest(WebKit.URISchemeRequest request)
    {
        var path = Uri.UnescapeDataString(request.GetPath()).TrimStart('/');
        if (IsOnFilesOrigin(request.GetUri()))
        {
            mounts ??= new MountTable(options.Mounts);
            var file = mounts.Open(path);
            if (file is null)
            {
                // With the CORS header, so the page's fetch sees the 404 instead of failing.
                Respond(request, [], "text/plain", status: 404, cacheControl: null, allowAppOrigin: true);
                return;
            }

            using (file.Content)
            {
                Respond(request, file.Content.ToArray(), file.ContentType, status: 200, file.CacheControl, allowAppOrigin: true);
            }

            return;
        }

        if (path.Length == 0)
        {
            path = "index.html";
        }

        using var stream = options.Frontend.TryOpen(path);
        if (stream is null)
        {
            Respond(request, [], "text/plain", status: 404, cacheControl: null, allowAppOrigin: false);
            return;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        Respond(request, memory.ToArray(), ContentTypes.FromPath(path), status: 200, cacheControl: null, allowAppOrigin: false);
    }

    private static void Respond(
        WebKit.URISchemeRequest request,
        byte[] content,
        string contentType,
        uint status,
        string? cacheControl,
        bool allowAppOrigin)
    {
        // An empty body (a 404) gets an empty stream: GirCore's GLib.Bytes throws for zero bytes.
        using var bytes = content.Length == 0 ? null : GLib.Bytes.New(content);
        using var input = bytes is null ? Gio.MemoryInputStream.New() : Gio.MemoryInputStream.NewFromBytes(bytes);
        using var response = WebKit.URISchemeResponse.New(input, content.Length);
        response.SetStatus(status, reasonPhrase: null);
        response.SetContentType(contentType);
        if (cacheControl is not null || allowAppOrigin)
        {
            var headers = Soup.MessageHeaders.New(Soup.MessageHeadersType.Response);
            if (cacheControl is not null)
            {
                headers.Append("Cache-Control", cacheControl);
            }

            if (allowAppOrigin)
            {
                headers.Append("Access-Control-Allow-Origin", Origin);
            }

            response.SetHttpHeaders(headers);
        }

        request.FinishWithResponse(response);
    }
}