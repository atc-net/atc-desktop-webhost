# Atc.Desktop.WebHost

A fast desktop host for .NET apps with a web frontend: a native window with a webview, a typed message bridge, and a startup pipeline built for time to first content.

| Package | What |
|---|---|
| `Atc.Desktop.WebHost` | Core: bridge, startup pipeline, single instance, settings, file watching, startup timing. No native dependencies. |
| `Atc.Desktop.WebHost.Windows` | Windows window: plain Win32 + WebView2. No WinForms, WPF or WinUI is loaded. |
| `Atc.Desktop.WebHost.Linux` | Linux window: GTK4 + WebKitGTK 6.0 through [GirCore](https://github.com/gircore/gir.core). Needs `libgtk-4-1` and `libwebkitgtk-6.0-4`. |

Status: early. Windows works and is measured. The Linux window runs (tested in WSL on Ubuntu with WebKitGTK 2.52) but has only been tested lightly.

## Quick start

```csharp
public static class Program
{
    [STAThread] // lets WebView2 run on the main thread
    public static int Main(string[] args)
    {
        StartupClock.Shared.Mark("main");

        using var app = new WebHostApplication(new WebHostOptions
        {
            AppId = "AtcSoft.Markdown.Viewer",
            Title = "Markdown Viewer",
            Platform = OperatingSystem.IsWindows() ? new WebView2Platform() : new WebKitGtkPlatform(),
            Frontend = FrontendSource.FromFolder(Path.Combine(AppContext.BaseDirectory, "wwwroot")),
            LoadDocument = async (path, ct) => JsonPayload.From(await MyRenderer.RenderAsync(path, ct), MyJson.Default.Rendered),
        });

        app.Bridge.Handle("md.render", MyJson.Default.RenderRequest, MyJson.Default.Rendered, MyRenderer.RenderAsync);
        return app.Run(args);
    }
}
```

The page talks to the host through `window.atcHost`, which the host injects before any page script runs:

```js
const { boot } = atcHost;                    // { theme, settings, document, documentPending, files, ... }
const doc = boot.document ?? await atcHost.request("host.document");
render(doc);
atcHost.on("file.changed", ({ path }) => reload(path));
atcHost.notify("host.firstPaint", { marks }); // startup measurement
```

## How startup works

1. **Single instance.** A named mutex is checked. A second launch hands its files to the running window over a named pipe and exits.
2. **The document loads in parallel.** The first file starts loading on the thread pool right away, with the app's `LoadDocument`.
3. **The window shows at once.** It is painted in the theme's background colour (light/dark from settings or the OS), so there is no white flash.
4. **The webview starts.** On WebView2 this is ~80% of startup.
5. **One boot script.** `window.atcHost` is injected together with settings, theme and the document, if it's ready. The page paints it without a round trip.
6. **The page loads from a private origin.** That's `https://app.atc-webhost.example/`, served natively from a folder with WebView2 virtual host mapping, or from embedded resources.

Every step is marked. Run the app with `--bench --bench-out file.txt` to get `STARTUP_MS` and every phase in the `KEY=VALUE` format of Per's `measure.ps1`. See [benchmarks/README.md](benchmarks/README.md).

## Bridge

Messages are JSON text:

| Direction | Message |
|---|---|
| page → host | `{ kind: "request", id, type, payload }`; no `id` means no response |
| page → host | `{ kind: "cancel", id }` |
| host → page | `{ kind: "response", id, ok, result \| error: { code, message } }` |
| host → page | `{ kind: "event", type, payload }` |

Handlers start on the UI thread. Typed handlers use source-generated `System.Text.Json` (trimming and AOT safe). Results are pre-serialized `JsonPayload` values, spliced into messages as-is.

Built-in requests:

| Group | Requests |
|---|---|
| Host | `host.ready`, `host.info`, `host.document`, `host.firstPaint` |
| Settings | `settings.get`, `settings.set` |
| Window and dialogs | `window.setTitle`, `dialog.openFile`, `dialog.saveFile` |
| Documents and files | `document.load`, `file.watch`, `file.unwatch` |
| Shell | `shell.openExternal` |

Built-in events: `settings.changed`, `file.changed`, `document.opened`.

## Sample

`sample/Atc.Desktop.WebHost.Sample` is a small text viewer. It demonstrates the host and is the app the benchmarks run.

```shell
dotnet run --project sample/Atc.Desktop.WebHost.Sample -- benchmarks/fixtures/readme-5k.md
dotnet run --project sample/Atc.Desktop.WebHost.Sample -- --ui=embedded --devtools
```

It includes a **Bridge bench** button, which measures request round trips (empty to 1 MB) and event throughput.

## Credits

The Win32 window and the WebKitGTK wiring are modelled on the C++ in [Photino.Native](https://github.com/tryphotino/photino.native) (Apache-2.0) and written from scratch in C#; no Photino code is copied.