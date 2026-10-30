namespace Atc.Desktop.WebHost.Windows;

/// <content>WebView2 startup, page loading and messages.</content>
internal sealed partial class Win32Window
{
    private CoreWebView2Environment? environment;
    private CoreWebView2Controller? controller;
    private CoreWebView2? webView;

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

        // SmartScreen checks every navigation online, also to our private origin. Measured on David's PC:
        // +1,030 ms per launch. The page is local content and external links leave the app, so it adds nothing.
        settings.IsReputationCheckingRequired = false;

        view.WebMessageReceived += OnWebMessageReceived;
        view.NavigationStarting += OnNavigationStarting;
        view.NewWindowRequested += OnNewWindowRequested;
        view.NavigationCompleted += (_, e) => events.Mark(e.IsSuccess ? "navigationCompleted" : "navigationFailed");
        view.DOMContentLoaded += (_, _) => events.Mark("domContentLoaded");
    }

    private void MapFrontend(CoreWebView2 view)
    {
        if (options.Frontend.FolderPath is { } folder)
        {
            // Native file serving: no managed code runs per request.
            view.SetVirtualHostNameToFolderMapping(FrontendSource.HostName, folder, CoreWebView2HostResourceAccessKind.Allow);
            return;
        }

        view.AddWebResourceRequestedFilter(FrontendSource.Origin + "/*", CoreWebView2WebResourceContext.All);
        view.WebResourceRequested += OnWebResourceRequested;
    }

    private void OnWebResourceRequested(
        object? sender,
        CoreWebView2WebResourceRequestedEventArgs e)
    {
        var path = FrontendSource.ToRelativePath(new Uri(e.Request.Uri));
        var stream = path is null ? null : options.Frontend.TryOpen(path);
        e.Response = stream is null
            ? environment!.CreateWebResourceResponse(Content: null, 404, "Not Found", Headers: string.Empty)
            : environment!.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: " + ContentTypes.FromPath(path!));
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

    /// <summary>The app's page stays in the window; links to other sites open in the default browser.</summary>
    private static void OnNavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (FrontendSource.IsOnOrigin(e.Uri))
        {
            return;
        }

        e.Cancel = true;
        ExternalLinks.TryOpen(e.Uri);
    }

    private static void OnNewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        ExternalLinks.TryOpen(e.Uri);
    }
}