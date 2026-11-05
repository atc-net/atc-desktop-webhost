namespace Atc.Desktop.WebHost;

/// <content>The built-in requests every app gets.</content>
public sealed partial class WebHostApplication
{
    private void RegisterBuiltInHandlers()
    {
        RegisterHostHandlers();
        RegisterShellHandlers();
        RegisterWindowHandlers();
        RegisterDialogHandlers();
        RegisterFileHandlers();
        RegisterFolderHandlers();
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
            return ExternalLinks.TryOpen(url)
                ? ValueTask.FromResult(JsonPayload.Null)
                : throw new BridgeException("bad_request", "Only http, https and mailto links are opened.");
        });
    }

    private void RegisterWindowHandlers()
    {
        Bridge.Handle("window.setTitle", (request, _) =>
        {
            RequireWindow().SetTitle(request.Deserialize(BridgeJsonContext.Default.TitleRequest).Title);
            return ValueTask.FromResult(JsonPayload.Null);
        });

        // Close guard: while armed, the user's close is turned into a window.closeRequested event.
        Bridge.Handle("window.setCloseGuard", (request, _) =>
        {
            closeGuard.SetArmed(request.Deserialize(BridgeJsonContext.Default.CloseGuardRequest).Enabled);
            return ValueTask.FromResult(JsonPayload.Null);
        });

        // Sent by the host script as soon as window.closeRequested arrives: the page is alive.
        Bridge.Handle("window.closeAck", (_, _) =>
        {
            closeGuard.Acknowledge();
            return ValueTask.FromResult(JsonPayload.Null);
        });

        Bridge.Handle("window.getMonitors", (_, _) => ValueTask.FromResult(JsonPayload.From(
            Options.Platform.GetMonitors(),
            BridgeJsonContext.Default.IReadOnlyListHostMonitor)));

        // Missing fields keep the current placement's value.
        Bridge.Handle("window.setPlacement", (request, _) =>
        {
            var change = request.Deserialize(BridgeJsonContext.Default.PlacementRequest);
            var window = RequireWindow();
            var current = window.Placement;
            window.SetPlacement(new WindowPlacement
            {
                Monitor = change.Monitor ?? current.Monitor,
                Fullscreen = change.Fullscreen ?? current.Fullscreen,
                ShowInTaskbar = change.ShowInTaskbar ?? current.ShowInTaskbar,
            });
            return ValueTask.FromResult(JsonPayload.Null);
        });

        Bridge.Handle("window.close", (_, _) =>
        {
            closeGuard.Reset();
            RequireWindow().Close();
            return ValueTask.FromResult(JsonPayload.Null);
        });
    }

    private void RegisterDialogHandlers()
    {
        Bridge.Handle("page.find", async (request, _) =>
        {
            var text = request.Payload.ValueKind == JsonValueKind.Object
                ? request.Deserialize(BridgeJsonContext.Default.FindRequest).Text
                : null;

            // Leave the webview's message callback before the find bar opens (as for dialogs).
            await Task.Yield();
            RequireWindow().Find(text);
            return JsonPayload.Null;
        });

        Bridge.Handle("page.print", async (_, _) =>
        {
            await Task.Yield();
            RequireWindow().Print();
            return JsonPayload.Null;
        });

        Bridge.Handle("dialog.openFile", async (request, _) =>
        {
            // Leave the webview's message callback first: a modal dialog runs its own message loop,
            // which WebView2 doesn't allow inside its event handlers (reentrancy).
            await Task.Yield();
            var path = await RequireWindow()
                .ShowOpenFileDialogAsync(ToDialogOptions(request, Options.OpenFileFilters))
                .ConfigureAwait(true);
            GrantPicked(path);
            return JsonPayload.From(new BuiltInMessages.FileDialogResult(path), BridgeJsonContext.Default.FileDialogResult);
        });

        Bridge.Handle("dialog.openFolder", async (request, _) =>
        {
            await Task.Yield();
            var path = await RequireWindow()
                .ShowOpenFolderDialogAsync(ToDialogOptions(request, []))
                .ConfigureAwait(true);
            if (path is not null)
            {
                GrantChosen(path, isFolder: true);
            }

            return JsonPayload.From(new BuiltInMessages.FileDialogResult(path), BridgeJsonContext.Default.FileDialogResult);
        });

        Bridge.Handle("dialog.saveFile", async (request, _) =>
        {
            await Task.Yield();
            var path = await RequireWindow()
                .ShowSaveFileDialogAsync(ToDialogOptions(request, []))
                .ConfigureAwait(true);
            GrantPicked(path);
            return JsonPayload.From(new BuiltInMessages.FileDialogResult(path), BridgeJsonContext.Default.FileDialogResult);
        });
    }

    private void RegisterFileHandlers()
    {
        Bridge.Handle("document.load", async (request, _) =>
        {
            var path = pathScope.Require(request.Deserialize(BridgeJsonContext.Default.PathRequest).Path);
            return await Task.Run(() => LoadDocumentAsync(path, isInitial: false), Lifetime).ConfigureAwait(true);
        });

        Bridge.Handle("file.watch", (request, _) =>
        {
            fileWatch.Watch(pathScope.Require(request.Deserialize(BridgeJsonContext.Default.PathRequest).Path));
            return ValueTask.FromResult(JsonPayload.Null);
        });

        // Answered by the host when the request carries files (see OnFilesReceived). Without files: no paths.
        Bridge.Handle("file.paths", (_, _) => ValueTask.FromResult(JsonPayload.From(
            Array.Empty<string>(),
            BridgeJsonContext.Default.IReadOnlyListString)));

        Bridge.Handle("file.unwatch", (request, _) =>
        {
            fileWatch.Unwatch(request.Deserialize(BridgeJsonContext.Default.PathRequest).Path);
            return ValueTask.FromResult(JsonPayload.Null);
        });
    }

    private void RegisterFolderHandlers()
    {
        Bridge.Handle("access.list", (_, _) => ValueTask.FromResult(JsonPayload.From(
            rememberedAccess?.GetAll() ?? [],
            BridgeJsonContext.Default.IReadOnlyListRememberedPath)));

        Bridge.Handle("access.forget", (request, _) =>
        {
            var path = request.Deserialize(BridgeJsonContext.Default.PathRequest).Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new BridgeException("bad_request", "A path is needed.");
            }

            rememberedAccess?.Forget(path);
            return ValueTask.FromResult(JsonPayload.Null);
        });

        Bridge.Handle("folder.list", async (request, cancellationToken) =>
        {
            var payload = request.Deserialize(BridgeJsonContext.Default.FolderListRequest);
            var folder = pathScope.Require(payload.Path);
            var maxFiles = payload.MaxFiles is > 0 and var max ? Math.Min(max, FolderLister.DefaultMaxFiles) : FolderLister.DefaultMaxFiles;
            var result = await Task.Run(
                    () => FolderLister.List(folder, payload.Pattern, payload.Recursive ?? false, maxFiles, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(true);
            return JsonPayload.From(result, BridgeJsonContext.Default.FolderListResult);
        });

        Bridge.Handle("folder.watch", (request, _) =>
        {
            var payload = request.Deserialize(BridgeJsonContext.Default.FolderWatchRequest);
            folderWatch.Watch(pathScope.Require(payload.Path), payload.Recursive ?? false);
            return ValueTask.FromResult(JsonPayload.Null);
        });

        Bridge.Handle("folder.unwatch", (request, _) =>
        {
            folderWatch.Unwatch(request.Deserialize(BridgeJsonContext.Default.PathRequest).Path);
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

    private void GrantPicked(string? path)
    {
        if (path is not null)
        {
            GrantChosen(path, isFolder: false);
        }
    }

    /// <summary>Gives the page a path the user chose, and remembers it for the next run.</summary>
    /// <param name="path">The file or folder.</param>
    /// <param name="isFolder">True for a folder.</param>
    private void GrantChosen(
        string path,
        bool isFolder)
    {
        GrantAccess(path, isFolder);
        rememberedAccess?.Record(path, isFolder);
    }

    private void GrantAccess(
        string path,
        bool isFolder)
    {
        if (isFolder)
        {
            pathScope.GrantFolder(path);
        }
        else
        {
            pathScope.Grant(path);
        }
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