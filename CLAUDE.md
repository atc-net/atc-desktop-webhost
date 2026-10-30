# CLAUDE.md

Guidance for Claude Code (and other agents) working in this repo.

## What this is

`Atc.Desktop.WebHost` is a fast desktop host for .NET apps with a web (TypeScript) frontend: a native window with a webview, a typed JSON message bridge, and a startup pipeline tuned for **time to first content**. It is the shared host for the ATC viewer apps (Markdown viewer first). The host knows nothing about file formats: each app passes a `LoadDocument(path)` function and its own bridge handlers.

Status: early. Windows works and is measured (first paint ~300–640 ms depending on how warm WebView2 is; in the latest comparison 464 ms for a 1 KB file and 1.3 s for 10 MB). It beats WPF + WebView2 and WinUI 3 + WebView2 on the same PC. Linux runs in WSL (Ubuntu, WebKitGTK 2.52): ~820 ms with software rendering. Only startup and bench mode have been exercised there.

To run on Linux from WSL, build from a clone in the WSL home folder (not `/mnt/d/...`), so Linux and Windows don't share `bin/` and `obj/`. Packages needed: `dotnet-sdk-10.0 libgtk-4-1 libwebkitgtk-6.0-4`.

## Layout

| Path | What |
|---|---|
| `src/Atc.Desktop.WebHost` | Core. No native dependencies, `IsAotCompatible`. Bridge, startup pipeline, single instance, settings, file watching, startup clock, bench output |
| `src/Atc.Desktop.WebHost.Windows` | Plain Win32 window + WebView2 (no WinForms/WPF/WinUI). Unsafe code and P/Invoke in `Interop/` |
| `src/Atc.Desktop.WebHost.Linux` | GTK4 + WebKitGTK 6.0 through GirCore (`GirCore.WebKit-6.0` preview) |
| `sample/Atc.Desktop.WebHost.Sample` | Demo text viewer (`atc-webhost-sample`). It is also the app the benchmarks run |
| `test/Atc.Desktop.WebHost.Tests` | Unit tests for the core only (xunit v3, Microsoft Testing Platform) |
| `benchmarks/` | `measure.ps1` (adapted from Per's), `Compare-Hosts.ps1`, `New-Fixtures.ps1`, `Test-Startup.ps1` (CI smoke test), `README.md` with results. `benchmarks/results/` and `fixtures/text-10mb.txt` are git-ignored |
| `benchmarks/hosts` | Comparison hosts (WPF + WebView2, WinUI 3 + WebView2) with their own `Hosts.slnx` and build settings. Not in the main solution or CI. Fairness rules in its README |

Solution file: `Atc.Desktop.WebHost.slnx`. Target: `net10.0`, C# 14.

## Commands

```shell
dotnet build Atc.Desktop.WebHost.slnx -c Release          # warnings are errors in Release
dotnet test --project test/Atc.Desktop.WebHost.Tests/Atc.Desktop.WebHost.Tests.csproj -c Release
dotnet run --project sample/Atc.Desktop.WebHost.Sample -- benchmarks/fixtures/readme-5k.md
dotnet run --project sample/Atc.Desktop.WebHost.Sample -- --ui=embedded --devtools
```

`global.json` selects the Microsoft Testing Platform, so `dotnet test` takes `--project`, not a positional path.

Benchmark (Windows, PowerShell). Every performance test uses both `text-1kb.txt` and `text-10mb.txt` (random line lengths, made by `New-Fixtures.ps1`):

```shell
pwsh benchmarks/Compare-Hosts.ps1      # this host vs WPF and WinUI 3, both files, order alternated over rounds
pwsh benchmarks/measure.ps1 -Exe sample/Atc.Desktop.WebHost.Sample/bin/Release/net10.0/atc-webhost-sample.exe `
    -Fixture benchmarks/fixtures/text-1kb.txt -Label 1kb -Runs 15
```

CI also runs `benchmarks/Test-Startup.ps1` on both OSes (Linux under `xvfb-run`): it starts the sample once with `--bench` and fails if the page doesn't report first paint.

The app's own switches: `--bench`, `--bench-out <file>`, `--new-instance`, `--devtools`. The sample adds `--ui=folder|embedded` and `--browser-args=...`.

## Architecture

**Startup pipeline** (`WebHostApplication.Run`): single-instance check (named mutex, files handed over a named pipe) → first document starts loading on the thread pool → native window shows at once in the theme background colour → webview starts → boot script injected → page loads from the private origin `https://app.atc-webhost.example/`. Every step calls `StartupClock.Mark(...)`; `--bench` writes them as `KEY=VALUE` lines and exits after the page's first paint.

**Platform abstraction** (`src/Atc.Desktop.WebHost/Platform`):
- `IHostPlatform`: creates the window and runs the message loop (blocks).
- `IHostWindow`: the small set of things the host needs (post message, title, bring to front, close, Open/Save dialogs, process ids). All members run on the UI thread. Keep it small and add members only when an app needs them.
- `IHostWindowEvents`: callbacks into the host, in startup order, on the UI thread. `WebHostApplication` implements it.

**Bridge** (`Bridge/WebBridge.cs`): JSON text messages.
- Page → host: `{ kind: "request", id, type, payload }`. A request without an `id` is a notification and gets no response. Cancel with `{ kind: "cancel", id }`.
- Host → page: `{ kind: "response", id, ok, result | error: { code, message } }` and `{ kind: "event", type, payload }`.
- Handlers start on the UI thread; move CPU-heavy work to the thread pool.
- Events are queued until the page sends `host.ready` (done automatically on `DOMContentLoaded`). Responses are never held back.
- Results are pre-serialized `JsonPayload` values, spliced into messages as-is. Throw `BridgeException(code, message)` for expected errors.
- Built-in handlers are in `WebHostApplication.Handlers.cs`; their DTOs are in `BuiltInMessages.cs` and `BridgeJsonContext.cs`.

**Page side** (`Hosting/HostScript.cs`): the injected shim defines `window.atcHost` (`platform`, `boot`, `request`, `notify`, `on`, `ready`), the same on WebView2 and WebKitGTK. Pages never touch `window.chrome.webview` or `window.webkit.messageHandlers` directly. If you change the shim, the boot object or the message shape, update `docs/host-contract.md` and keep `@atc-net/viewer-host` (the TS package, in another repo) in mind. Removing or renaming anything there is a breaking change.

**File access** (`Files/PathScope.cs`): `document.load` and `file.watch` only accept files in or below the folder of a file the user chose (command line, second launch, Open/Save dialog); otherwise `forbidden`. Apps widen it with `GrantFileAccess`. Any new handler that takes a path from the page must go through `pathScope.Require`.

**Folders** (`Hosting/HostPaths.cs`): settings in `%APPDATA%\{Vendor}\{AppId}`, data and the WebView2 profile in `%LOCALAPPDATA%\{Vendor}\{AppId}` (XDG on Linux). A `portable.flag` file next to the exe puts both in `data\` beside it.

## Conventions

- ATC coding rules apply (`.editorconfig` + `Directory.Build.props` per folder, from `atc-coding-rules-updater`). The analyzers are Atc.Analyzer, StyleCop, Meziantou, Sonar and AsyncFixer, with `AnalysisLevel=latest-All`. **Release builds must have 0 warnings.** Don't edit the generated rule files by hand; re-run `atc-coding-rules-updater.ps1` instead.
- File-scoped namespaces, one type per file, `GlobalUsings.cs` per project (add new `using`s there).
- Methods with more than one parameter put each parameter on its own line (see the existing code).
- Expression-bodied members put `=>` on the next line.
- Trailing commas in multi-line initializers (SA1413 is an error).
- XML docs on public API. Write short, plain sentences.
- **Public API is tracked** (`Microsoft.CodeAnalysis.PublicApiAnalyzers`). Every public member of the `src` projects is listed in its `PublicAPI.Unshipped.txt`; a missing or stale line fails the build (RS0016/RS0017). Add lines by hand or run `dotnet format analyzers src/<project>/<project>.csproj --diagnostics RS0016 --severity info`. When a version is released, move the lines to `PublicAPI.Shipped.txt`. A removed or changed line in `Shipped` is a breaking change.
- JSON goes through **source-generated** `System.Text.Json` contexts (`JsonTypeInfo<T>`). No reflection-based serialization in `src/` (keep it trimming/AOT safe).
- `ConfigureAwait(true)` where code must return to the UI thread (bridge, handlers). `ConfigureAwait(false)` for thread-pool work.
- The core gives `InternalsVisibleTo` to the tests and both platform projects. Internal is fine for host plumbing.
- Tests: `[Fact]`, names like `Subject_Expectation` (e.g. `UnknownType_IsAnsweredWithAnError`), one test class per type in a folder that mirrors `src/`, AwesomeAssertions (`.Should()`), NSubstitute, AutoFixture via Atc.Test.
- The root `Directory.Build.targets` imports the Windows project's `buildTransitive` target. It removes WebView2's WinForms/WPF references (otherwise MSB3277 fails the Release build).

## Things to know

- **Performance is the product.** Before changing anything on the startup path, measure it with `measure.ps1` or `Compare-Hosts.ps1` on both text files (warm runs, median; expect ±40 ms noise) and update `benchmarks/README.md`. The README's comparison table is measured; keep it that way.
- **Comparison hosts must follow the fairness rules** in `benchmarks/hosts/README.md` (same page, clock, webview settings, profile and tuning). Don't optimize them beyond what the framework's template does, and don't hobble them either.
- **SmartScreen is off** (`IsReputationCheckingRequired = false` in `Win32Window.WebView.cs`). It cost about 1 s per launch. This choice is still waiting for Per's OK.
- **The boot script isn't awaited before `Navigate`** (saves ~30 ms). This relies on WebView2 handling the calls in order, which isn't documented. If `atcHost` is ever missing, await it first.
- **Open/Save dialogs** run from the window's message loop, not inside a WebView2 event handler, to avoid a hang.
- **One window per process.** `Win32Window` enforces it.
- **The Windows window is modelled on Photino.Native's C++, but no code is copied.** Keep it that way (license and credits in the README).
- **Linux:** the webview profile is in the app's data folder (`WebKit.NetworkSession`), the system theme comes from `GTK_THEME` or GNOME's `color-scheme`/`gtk-theme` settings (~10 ms, only when the theme setting is `system`), and the webview's process ids are this process's descendants in `/proc` (WebKit has no API for them). Still different from Windows: origin `atc-app://app/` (not `https`). External links are handled like on Windows, but that hasn't been clicked through on Linux yet.
- `FileWatchServiceTests.SeveralWrites_AreReportedOnce` depends on timing and may be flaky on slow CI runners.
- ReadyToRun (`dotnet publish sample/Atc.Desktop.WebHost.Sample -c Release -r win-x64`) gives no measurable startup gain, only a smaller install.
- WebView2 start varies up to 4× between sessions on the same PC (total ~300 ms vs ~600 ms). Only compare variants measured back to back in the same session.

## Commits

Nothing committed to this repo may refer to plans, issues or `atc-roadmaps`. This covers the commit message (subject and body) and every committed file: code, comments, docs, the README and this file.
- No IDs for plan tasks, issues, proposals or decisions (anything like `XX-12`, `I-3`, `P-2`).
- No paths or links to `plans\*`, `issues\*` or `*\atc-roadmaps\*`, and no mention of plan files, phases or items.
- Describe what the change does on its own terms.

Before committing, search the staged files for IDs like these and for the words roadmap, brainstorm and issues.md.

Per (colleague) owns the Markdown viewer R&D (`atc-soft-markdown-viewer`, branch `feature/init`): the WinUI 3 spike, the original `measure.ps1` and the 469 ms ReadyToRun baseline.
