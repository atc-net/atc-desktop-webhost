# 🆚 Comparison hosts

Small apps that do what the sample does, in other UI frameworks, so `Atc.Desktop.WebHost` can be compared with them on the same PC.

| Folder | Host | Exe |
|---|---|---|
| `Wpf` | WPF + WebView2 (`Microsoft.Web.WebView2.Wpf`) | `atc-bench-wpf.exe` |
| `WinUI` | WinUI 3 + WebView2, unpackaged, Windows App SDK bundled | `atc-bench-winui.exe` |

They are not part of `Atc.Desktop.WebHost.slnx` or CI. They have their own `Hosts.slnx` and build settings (no analyzers, no warnings as errors). The WPF host also builds on Linux (`EnableWindowsTargeting`); the WinUI 3 host builds on Windows only.

## ▶️ Run the comparison

```shell
pwsh benchmarks/Compare-Hosts.ps1                       # all hosts, text-1kb.txt and text-10mb.txt
pwsh benchmarks/Compare-Hosts.ps1 -Rounds 4 -Runs 10    # more rounds for steadier numbers
pwsh benchmarks/Compare-Hosts.ps1 -Hosts webhost,wpf -Fixtures readme-5k.md
```

The script builds the sample and the hosts, measures each one with `measure.ps1` and writes a markdown table per file to `benchmarks/results/compare/compare.md`.

## ⚖️ Fairness rules

Every host must follow these, or the numbers mean nothing:

1. **Same page.** The hosts serve the sample's own `wwwroot` (linked, not copied). A small injected `window.atcHost` stand-in gives the page its boot data, so the page code and its render path are identical.
2. **Same clock.** `STARTUP_MS` is from the moment the OS started the process to the moment the host *receives* the page's first-paint message, computed like the host's `StartupClock`. Starting the clock in `Main` would hide the runtime start.
3. **Same first-paint rule.** Two animation frames after the document is in the DOM (the sample page does this).
4. **Same webview settings.** SmartScreen off (`IsReputationCheckingRequired = false`), the same settings as the host, UI served by virtual host mapping from the same origin. `Source` is never set in XAML, because that would navigate before the settings are applied.
5. **Document read in parallel.** The file is read on the thread pool from the first line of app code and goes into the boot script, so the page needs no round trip.
6. **Own, persistent profile** per host (`%LOCALAPPDATA%\AtcBench\<host>`), so warm launches are warm.
7. **Same build tuning.** The startup settings of the sample's project file (invariant globalization, no concurrent GC, ...).
8. **Measured in one session, order alternated.** WebView2 start drifts, so `Compare-Hosts.ps1` reverses the order every other round.

What the hosts do the standard way, as an app written from the framework's template would:

- WPF and WinUI 3 use `App.xaml` and `MainWindow.xaml`, with the generated `Main`. WinUI 3 merges `XamlControlsResources`, as its template does.
- The webview is created with `EnsureCoreWebView2Async`, and the boot script is awaited before navigating, as the WebView2 documentation shows. The host instead starts navigating without waiting (about 30 ms faster). That's one of its optimizations, so it's part of what's measured.

## 📄 Test files

Made by `benchmarks/New-Fixtures.ps1` with a fixed seed:

| File | Size | Lines |
|---|---|---|
| `text-1kb.txt` | 1 KB | 20 |
| `text-10mb.txt` | 10 MB (not committed; created on demand) | ~103,000, random lengths 0–4,000 characters |
