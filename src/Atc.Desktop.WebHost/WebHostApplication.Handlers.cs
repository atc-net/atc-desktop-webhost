namespace Atc.Desktop.WebHost;

/// <content>The built-in requests every app gets.</content>
public sealed partial class WebHostApplication
{
    private void RegisterBuiltInHandlers()
    {
        RegisterHostHandlers();
        RegisterShellHandlers();
        RegisterWindowHandlers();
        RegisterFileHandlers();
    }

    private void RegisterHostHandlers()
    {
        Bridge.Handle("host.ready", (_, _) =>
        {
            Clock.Mark("pageReady");
            Bridge.OpenEvents();
            return ValueTask.FromResult(JsonPayload.Null);
        });

        Bridge.Handle("host.info", (_, _) => ValueTask.FromResult(JsonPayload.From(
            new BuiltInMessages.HostInfo(
                Options.AppId,
                Options.Platform.Name,
                typeof(WebHostApplication).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                Environment.Version.ToString(),
                Clock.SinceProcessStart()),
            BridgeJsonContext.Default.HostInfo)));

        Bridge.Handle("host.document", async (_, cancellationToken) =>
            initialDocument is null
                ? JsonPayload.Null
                : await initialDocument.WaitAsync(cancellationToken).ConfigureAwait(true));

        Bridge.Handle("host.firstPaint", (request, _) =>
        {
            Clock.Mark("firstPaint");
            var marks = request.Payload.ValueKind == JsonValueKind.Object
                ? request.Deserialize(BridgeJsonContext.Default.FirstPaintRequest).Marks
                : null;
            pageMarks = marks?.ToDictionary(m => m.Key, m => Clock.FromEpochMs(m.Value), StringComparer.Ordinal)
                        ?? new Dictionary<string, double>(StringComparer.Ordinal);
            if (commandLine.IsBench)
            {
                WriteBenchReport();
                Window?.Close();
            }

            return ValueTask.FromResult(JsonPayload.Null);
        });

        RegisterSettingsHandlers();
    }

    private void RegisterSettingsHandlers()
    {
        Bridge.Handle("settings.get", (_, _) => ValueTask.FromResult(JsonPayload.From(Settings.Get())));

        Bridge.Handle("settings.set", (request, _) =>
        {
            var patch = request.Payload.ValueKind == JsonValueKind.Object
                ? JsonObject.Create(request.Payload.Clone()) ?? []
                : throw new BridgeException("bad_request", "settings.set needs an object.");
            var merged = JsonPayload.From(Settings.Merge(patch));
            Bridge.Emit("settings.changed", merged);
            return ValueTask.FromResult(merged);
        });
    }

    private void RegisterShellHandlers()
    {
        Bridge.Handle("shell.openExternal", (request, _) =>
        {
            var url = request.Deserialize(BridgeJsonContext.Default.UrlRequest).Url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https" or "mailto"))
            {
                throw new BridgeException("bad_request", "Only http, https and mailto links are opened.");
            }

            using var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return ValueTask.FromResult(JsonPayload.Null);
        });
    }

    private void RegisterWindowHandlers()
    {
        Bridge.Handle("window.setTitle", (request, _) =>
        {
            RequireWindow().SetTitle(request.Deserialize(BridgeJsonContext.Default.TitleRequest).Title);
            return ValueTask.FromResult(JsonPayload.Null);
        });

        Bridge.Handle("dialog.openFile", async (request, _) =>
        {
            // Leave the webview's message callback first: a modal dialog runs its own message loop,
            // which WebView2 doesn't allow inside its event handlers (reentrancy).
            await Task.Yield();
            var path = await RequireWindow()
                .ShowOpenFileDialogAsync(ToDialogOptions(request, Options.OpenFileFilters))
                .ConfigureAwait(true);
            return JsonPayload.From(new BuiltInMessages.FileDialogResult(path), BridgeJsonContext.Default.FileDialogResult);
        });

        Bridge.Handle("dialog.saveFile", async (request, _) =>
        {
            await Task.Yield();
            var path = await RequireWindow()
                .ShowSaveFileDialogAsync(ToDialogOptions(request, []))
                .ConfigureAwait(true);
            return JsonPayload.From(new BuiltInMessages.FileDialogResult(path), BridgeJsonContext.Default.FileDialogResult);
        });
    }

    private void RegisterFileHandlers()
    {
        Bridge.Handle("document.load", async (request, _) =>
        {
            var path = Path.GetFullPath(request.Deserialize(BridgeJsonContext.Default.PathRequest).Path);
            return await Task.Run(() => LoadDocumentAsync(path, isInitial: false), Lifetime).ConfigureAwait(true);
        });

        Bridge.Handle("file.watch", (request, _) =>
        {
            fileWatch.Watch(request.Deserialize(BridgeJsonContext.Default.PathRequest).Path);
            return ValueTask.FromResult(JsonPayload.Null);
        });

        Bridge.Handle("file.unwatch", (request, _) =>
        {
            fileWatch.Unwatch(request.Deserialize(BridgeJsonContext.Default.PathRequest).Path);
            return ValueTask.FromResult(JsonPayload.Null);
        });
    }

    private static FileDialogOptions ToDialogOptions(
        BridgeRequest request,
        IReadOnlyList<FileDialogFilter> defaultFilters)
    {
        var payload = request.Payload.ValueKind == JsonValueKind.Object
            ? request.Deserialize(BridgeJsonContext.Default.FileDialogRequest)
            : null;
        return new FileDialogOptions
        {
            Title = payload?.Title,
            DefaultPath = payload?.DefaultPath,
            Filters = payload?.Filters is { Count: > 0 } filters ? filters : defaultFilters,
        };
    }

    private IHostWindow RequireWindow()
        => Window ?? throw new BridgeException("not_ready", "The window isn't created yet.");

    private void WriteBenchReport()
    {
        var memory = BenchReport.MeasureMemory(Window?.GetBrowserProcessIds() ?? []);
        var report = BenchReport.Format(Clock.SinceProcessStart(), pageMarks, memory, Options.Platform.Name);
        if (commandLine.BenchOut is { } path)
        {
            File.WriteAllText(path, report);
        }
        else
        {
            Console.Out.Write(report);
        }
    }
}