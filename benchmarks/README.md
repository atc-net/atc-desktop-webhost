# 📊 Benchmarks

Startup is measured from the moment the OS starts the process to the page's first content paint. That is two animation frames after the document is in the DOM, the same rule the WinUI 3 spike uses.

Every performance test runs with two plain-text files with random line lengths: `text-1kb.txt` (startup with almost nothing to show) and `text-10mb.txt` (startup with a large document). The Markdown-like `readme-*.md` files are still there for older comparisons.

```shell
pwsh benchmarks/New-Fixtures.ps1                      # text-1kb.txt, text-10mb.txt (not committed), readme-500/5k/50k.md
pwsh benchmarks/Compare-Hosts.ps1                     # this host vs WPF and WinUI 3, both text files
dotnet build sample/Atc.Desktop.WebHost.Sample -c Release
pwsh benchmarks/measure.ps1 -Exe sample/Atc.Desktop.WebHost.Sample/bin/Release/net10.0/atc-webhost-sample.exe `
    -Fixture benchmarks/fixtures/text-1kb.txt -Label 1kb -Runs 15
pwsh benchmarks/measure.ps1 ... -Fixture benchmarks/fixtures/text-10mb.txt -Label 10mb
```

`measure.ps1` is adapted from the one in atc-soft-markdown-viewer (`feature/init`). It does one warm-up launch and then reports the median and p90, with every phase median and every run's values in `benchmarks/results/<label>.json` (not committed).

## 🆚 Compared with WPF and WinUI 3 (2026-10-30)

`Compare-Hosts.ps1` with the hosts in [hosts/](hosts/README.md): the same page, file, webview settings and clock in every host. Intel Core Ultra 7 366H laptop, Windows 11, JIT Release builds, 2 rounds × 10 warm runs per host, order reversed in the second round. Times are ms since the process started.

**text-1kb.txt**

| Host | Median | p90 | App code starts | Window created | WebView2 ready | Memory | Build output |
|---|---|---|---|---|---|---|---|
| ⚡ **Atc.Desktop.WebHost** | **464 ms** | 498 ms | 54 | 109 | 356 | 161 MB | 36.9 MB |
| WinUI 3 + WebView2 | 609 ms | 658 ms | 134 | 217 | 496 | 226 MB | 155.2 MB |
| WPF + WebView2 | 852 ms | 949 ms | 104 | 193 | 752 | 316 MB | 2.3 MB + .NET desktop runtime |

**text-10mb.txt**

| Host | Median | p90 | WebView2 ready | Memory |
|---|---|---|---|---|
| ⚡ **Atc.Desktop.WebHost** | **1,310 ms** | 1,447 ms | 366 | 998 MB |
| WinUI 3 + WebView2 | 1,474 ms | 1,484 ms | 507 | 1,035 MB |
| WPF + WebView2 | 1,717 ms | 1,799 ms | 755 | 1,101 MB |

What the numbers say:

- 🏁 **The whole lead is won before WebView2 starts.** From navigation to first paint, all three take about the same time. WinUI 3 reaches app code ~80 ms later (Windows App SDK start) and has WebView2 ready ~140 ms later. WPF's WebView2 control only starts the browser once the WPF window has rendered (~490 ms), so its WebView2 is ready ~400 ms later.
- 📄 **A 10 MB file costs ~900 ms in every host**: ~170 ms to hand the text to the page (the boot script), then ~720 ms for the browser to lay out 10 MB in one `<pre>`. That's page work, not host work. A viewer for large files needs a virtualized view; the host can't fix it.
- 🧠 **Memory** is mostly the 10 MB text in the page (~1 GB in every host). The host process itself is smallest here.
- 🎲 **Session drift:** the host's 1 KB median was 464 ms this session vs ~300 ms on 2026-10-29 (see below). Only compare numbers from one session.

## 🏆 Results

David's PC, Windows 11 Enterprise 26200, WebView2 runtime 156.0.4314.50, 5,000-line fixture, warm launches, **JIT build** (Release, not ReadyToRun: `dotnet publish` wasn't available in the session). Medians of 8–10 runs. Expect ±40 ms noise between runs of the same variant.

| Variant | Median | Notes |
|---|---|---|
| 🐌 SmartScreen on (WebView2 default) | 1,590 ms | ~1,030 ms is SmartScreen checking the navigation to our own origin |
| ⚡ **SmartScreen off, folder (virtual host mapping)** | **~570–640 ms** | Default now |
| 📦 SmartScreen off, embedded resources (`WebResourceRequested`) | ~740 ms | +100 ms: managed code runs for every file request |
| Boot script awaited before `Navigate` | 616 ms | vs 586 ms when not awaited (same binary, A/B) |
| `--disable-gpu`, `--disable-background-networking`, `--disable-features=msWebOOUI,msPdfOOUI` | ~570 ms each | No difference beyond noise |

⏱️ Where the time goes (folder, median):

| Phase | ms | |
|---|---|---|
| Process start → `Main` | 63 | .NET runtime start. ReadyToRun didn't change it (see below) |
| `Main` → window visible | 72 | Runtime start and native window creation; ReadyToRun didn't change it either |
| WebView2 environment | 16 | |
| 🔥 WebView2 controller (browser process) | 325 | The largest phase; not in our code |
| Configure + boot script + navigate | 60 → 30 | After the A/B change above |
| Navigate → page script runs | 20 | |
| Script → content in DOM | 10 | 102 KB document already in the boot data |
| Content → first paint | ~45 | Two animation frames |

🧠 Memory at first paint: host 16 MB, WebView2 processes ~176 MB (private bytes).

### ⚙️ ReadyToRun vs JIT (2026-10-29, later session)

Same PC, SmartScreen off, UI from a folder, 15 runs each, run back to back as JIT → ReadyToRun → JIT. ReadyToRun is `dotnet publish sample/Atc.Desktop.WebHost.Sample -c Release -r win-x64`; its DLLs are about twice the size, so the precompiled code is there.

| Build | Median | p90 | Install |
|---|---|---|---|
| JIT (Release build output) | 313 / 292 ms | 342 / 577 ms | 36.9 MB |
| **ReadyToRun (publish)** | **293 ms** (310 ms in an earlier run) | 380 ms | 18.7 MB |

🤷 **ReadyToRun makes no measurable difference.** The two .NET phases are the same in both builds: process start → `Main` ~55 ms, `Main` → window visible ~65 ms. So that time isn't JIT. It's runtime start-up and native window creation. The only gain is a smaller install: 18.7 MB published vs 36.9 MB of build output, which also carries the Linux and GirCore files.

⚠️ **The whole app was about twice as fast as in the first session** (~300 ms vs ~600 ms). The difference is almost all in WebView2 controller creation: **~70–85 ms now vs ~335 ms before**. Our own phases didn't change. Other WebView2 processes from another app were running during this session, so the WebView2 runtime files were probably already in memory. That isn't proven. Until it is, treat ~300 ms as a warm-machine number and ~600 ms as a colder one, and compare variants only within one session. Navigate → first paint was ~80 ms in both sessions. Configure + navigate is now ~11 ms.

WebView2 memory at first paint was ~240 MB this session (~176 MB before).

### 🐧 Linux (WebKitGTK), first runs (2026-10-29)

WSL on David's PC: Ubuntu, GTK 4.22, WebKitGTK 2.52, .NET 10.0.5, JIT Release build, 5,000-line fixture. Run directly with `--bench` (`measure.ps1` is Windows-only so far). 🖥️ **Software rendering:** the WSL user can't open `/dev/dri` (not in the `video`/`render` groups), so these are worst-case paint numbers.

| Run | First paint |
|---|---|
| Very first launch (cold) | 2,256 ms |
| 5 warm launches | 800–871 ms (median 822 ms) |

⏱️ Where the time goes (warm, typical run):

| Phase | ms | |
|---|---|---|
| Process start → `Main` | ~22 | Faster than on Windows (~55 ms) |
| `Main` → GTK initialized | ~150 | Loading GTK and WebKitGTK through GirCore |
| GTK initialized → window created | ~105 | |
| Window → navigate | ~35 | |
| 🔥 Navigate → page script runs | ~370 | WebKit's web process starting; the largest phase, outside our code |
| Script → content in DOM | ~20 | |
| Content → first paint | ~110 | Two animation frames with software rendering |

🧠 Memory: ~190 MB private in the host process, which includes GTK and WebKit's UI side (in-process on Linux), plus ~170 MB in WebKit's own processes (web, network and sandbox helpers), ~360 MB in total.

## 🔭 Next measurements

- 🎲 **Why WebView2 controller creation varies 4×.** Measure with and without other WebView2 apps running (Teams, Outlook, Widgets), and after a reboot.
- 🆚 **More hosts.** Avalonia (it needs a webview control first) and WPF drawing the text itself, without a webview. Run on more PCs, and with ReadyToRun builds of every host.
- 🏗️ **Native AOT.** `Microsoft.Web.WebView2.Core` uses built-in COM interop, which AOT doesn't support. That needs a ComWrappers-based binding first. Low priority: ReadyToRun showed the .NET phases aren't JIT-bound.
- 🎮 **Linux with GPU rendering.** In WSL, add the user to the `video` and `render` groups and measure again. Better still, measure on a real Linux machine. Make `measure.ps1` run under `pwsh` on Linux.