namespace Atc.Desktop.WebHost.Windows;

/// <content>WebView2 startup, page loading and messages.</content>
internal sealed partial class Win32Window
{
    private CoreWebView2Environment? environment;
    private CoreWebView2Controller? controller;
    private CoreWebView2? webView;
    private MountTable? mounts;

    public void PostMessage(string json)
        => webView?.PostWebMessageAsJson(json);

    public IReadOnlyList<int> GetBrowserProcessIds()
        => environment is null
            ? []
            : [.. environment.GetProcessInfos().Select(p => p.ProcessId)];

    private async Task InitializeWebViewAsync()
    {
        try
        {
            events.Mark("webviewStarting");
            var environmentOptions = new CoreWebView2EnvironmentOptions(options.AdditionalBrowserArguments);
            environment = await CoreWebView2Environment
                .CreateAsync(browserExecutableFolder: null, options.DataFolder, environmentOptions)
                .ConfigureAwait(true);
            events.Mark("webviewEnvironmentCreated");

            // The boot script may wait briefly for the document; let that overlap controller creation.
            var bootScript = events.CreateDocumentScriptAsync().AsTask();

            controller = await environment.CreateCoreWebView2ControllerAsync(hwnd).ConfigureAwait(true);
            events.Mark("webviewControllerCreated");
            if (isClosed)
            {
                return;
            }

            var color = options.BackgroundColor;
            controller.DefaultBackgroundColor = Color.FromArgb(255, color.R, color.G, color.B);
            OnResized(minimized: NativeMethods.IsIconic(hwnd));
            webView = controller.CoreWebView2;
            controller.AcceleratorKeyPressed += OnAcceleratorKeyPressed;
            webView.ContainsFullScreenElementChanged += (_, _) => OnFullscreenElementChanged();
            ConfigureWebView(webView);
            MapFrontend(webView);
            events.Mark("webviewConfigured");

            // Calls on one CoreWebView2 are handled in order, so the script is registered before the
            // navigation starts. Not awaiting it first saves a round trip (measured: -30 ms median).
            var addScript = webView.AddScriptToExecuteOnDocumentCreatedAsync(await bootScript.ConfigureAwait(true));
            webView.Navigate(FrontendSource.UrlFor(options.EntryPath).AbsoluteUri);
            events.Mark("navigationStarted");
            await addScript.ConfigureAwait(true);
            events.Mark("bootScriptAdded");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!isClosed)
            {
                ShowFatalError(ex);
            }
        }
    }

    private void ConfigureWebView(CoreWebView2 view)
    {
        var settings = view.Settings;
        settings.AreDevToolsEnabled = options.DevToolsEnabled;
        settings.AreDefaultContextMenusEnabled = options.DevToolsEnabled;
        settings.AreBrowserAcceleratorKeysEnabled = options.DevToolsEnabled;
        settings.IsStatusBarEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.AreHostObjectsAllowed = false;

        settings.AreBrowserAcceleratorKeysEnabled = options.DevToolsEnabled;
        settings.IsStatusBarEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.AreHostObjectsAllowed = false;

        // SmartScreen checks every navigation online, also to our private origin. Measured on David's PC:
        // +1,030 ms per launch. The page is local content and external links leave the app, so it adds nothing.
        settings.IsReputationCheckingRequired = false;

        view.WebMessageReceived += OnWebMessageReceived;
        view.NavigationStarting += OnNavigationStarting;
        view.NewWindowRequested += OnNewWindowRequested;
        view.ProcessFailed += (_, _) => events.OnPageUnloaded();
        view.NavigationCompleted += (_, e) => events.Mark(e.IsSuccess ? "navigationCompleted" : "navigationFailed");
        view.DOMContentLoaded += (_, _) => events.Mark("domContentLoaded");
    }

    private void MapFrontend(CoreWebView2 view)
    {
        var serveFrontend = options.Frontend.FolderPath is null;
        if (options.Frontend.FolderPath is { } folder)
        {
            // Native file serving: no managed code runs per request. WebView2 then never raises
            // WebResourceRequested for this host name, which is why mounts have their own host name.
            view.SetVirtualHostNameToFolderMapping(FrontendSource.HostName, folder, CoreWebView2HostResourceAccessKind.Allow);
        }
        else
        {
            view.AddWebResourceRequestedFilter(FrontendSource.Origin + "/*", CoreWebView2WebResourceContext.All);
        }

        if (options.Mounts.Count > 0)
        {
            mounts = new MountTable(options.Mounts);
            view.AddWebResourceRequestedFilter(FrontendSource.FilesOrigin + "/*", CoreWebView2WebResourceContext.All);
        }

        if (serveFrontend || mounts is not null)
        {
            view.WebResourceRequested += OnWebResourceRequested;
        }
    }

    /// <summary>One handler for both hosts: a second handler would answer the other host's requests with 404.</summary>
    private void OnWebResourceRequested(
        object? sender,
        CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = new Uri(e.Request.Uri);
        if (mounts is not null && string.Equals(uri.Host, FrontendSource.FilesHostName, StringComparison.OrdinalIgnoreCase))
        {
            var file = mounts.Open(Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/'));
            e.Response = file is null
                ? environment!.CreateWebResourceResponse(Content: null, 404, "Not Found", Headers: string.Empty)
                : environment!.CreateWebResourceResponse(file.Content, 200, "OK", MountHeaders(file));
            return;
        }

        var path = FrontendSource.ToRelativePath(uri);
        var stream = path is null ? null : options.Frontend.TryOpen(path);
        e.Response = stream is null
            ? environment!.CreateWebResourceResponse(Content: null, 404, "Not Found", Headers: string.Empty)
            : environment!.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: " + ContentTypes.FromPath(path!));
    }

    /// <summary>The app's page may also fetch mounted files (it's another origin).</summary>
    private static string MountHeaders(MountedFile file)
    {
        var headers = new StringBuilder()
            .Append("Content-Type: ").Append(file.ContentType)
            .Append("\r\nAccess-Control-Allow-Origin: ").Append(FrontendSource.Origin);
        if (file.CacheControl is not null)
        {
            headers.Append("\r\nCache-Control: ").Append(file.CacheControl);
        }

        return headers.ToString();
    }

    private void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!FrontendSource.IsOnOrigin(e.Source))
        {
            return;
        }

        string message;
        try
        {
            message = e.TryGetWebMessageAsString();
        }
        catch (ArgumentException)
        {
            message = e.WebMessageAsJson;
        }

        events.OnMessage(message);
    }

    /// <summary>
    /// The app's page stays in the window; links to other sites open in the default browser.
    /// A navigation within the app (a reload) replaces the page, so the old page's state is gone.
    /// </summary>
    private void OnNavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (FrontendSource.IsOnOrigin(e.Uri))
        {
            events.OnPageUnloaded();
            return;
        }

        e.Cancel = true;
        if (!IsOnFilesOrigin(e.Uri))
        {
            ExternalLinks.TryOpen(e.Uri);
        }
    }

    // A mounted page may not replace the app's page, and its address means nothing to a browser.
    private static bool IsOnFilesOrigin(string address)
        => Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
           string.Equals(uri.Host, FrontendSource.FilesHostName, StringComparison.OrdinalIgnoreCase);

    private static void OnNewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        ExternalLinks.TryOpen(e.Uri);
    }
}