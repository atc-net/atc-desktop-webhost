# Benchmarks

Startup is measured from the moment the OS starts the process to the page's first content paint. That is two animation frames after the document is in the DOM, the same rule Per's spike uses.

```shell
pwsh benchmarks/New-Fixtures.ps1                      # readme-500.md, readme-5k.md, readme-50k.md
dotnet build sample/Atc.Desktop.WebHost.Sample -c Release
pwsh benchmarks/measure.ps1 -Exe sample/Atc.Desktop.WebHost.Sample/bin/Release/net10.0/atc-webhost-sample.exe `
    -Fixture benchmarks/fixtures/readme-5k.md -Label folder -Runs 15
pwsh benchmarks/measure.ps1 ... -Label embedded -ExtraArgs "--ui=embedded"
```

`measure.ps1` is adapted from Per's (atc-soft-markdown-viewer `feature/init`). It does one warm-up launch and then reports the median and p90, with every phase median in `benchmarks/results/<label>.json` (not committed).

## Results

David's PC, Windows 11 Enterprise 26200, WebView2 runtime 156.0.4314.50, 5,000-line fixture, warm launches, **JIT build** (Release, not ReadyToRun: `dotnet publish` wasn't available in the session). Medians of 8–10 runs. Expect ±40 ms noise between runs of the same variant.

| Variant | Median | Notes |
|---|---|---|
| SmartScreen on (WebView2 default) | 1,590 ms | ~1,030 ms is SmartScreen checking the navigation to our own origin |
| **SmartScreen off, folder (virtual host mapping)** | **~570–640 ms** | Default now |
| SmartScreen off, embedded resources (`WebResourceRequested`) | ~740 ms | +100 ms: managed code runs for every file request |
| Boot script awaited before `Navigate` | 616 ms | vs 586 ms when not awaited (same binary, A/B) |
| `--disable-gpu`, `--disable-background-networking`, `--disable-features=msWebOOUI,msPdfOOUI` | ~570 ms each | No difference beyond noise |

Where the time goes (folder, median):

| Phase | ms | |
|---|---|---|
| Process start → `Main` | 63 | .NET runtime start (Per: 38 ms). ReadyToRun didn't change it (see below) |
| `Main` → window visible | 72 | Runtime start and native window creation; ReadyToRun didn't change it either |
| WebView2 environment | 16 | |
| WebView2 controller (browser process) | 325 | The largest phase; not in our code |
| Configure + boot script + navigate | 60 → 30 | After the A/B change above |
| Navigate → page script runs | 20 | |
| Script → content in DOM | 10 | 102 KB document already in the boot data |
| Content → first paint | ~45 | Two animation frames |

Memory at first paint: host 16 MB, WebView2 processes ~176 MB (private bytes).

### ReadyToRun vs JIT (2026-10-29, later session)

Same PC, SmartScreen off, UI from a folder, 15 runs each, run back to back as JIT → ReadyToRun → JIT. ReadyToRun is `dotnet publish sample/Atc.Desktop.WebHost.Sample -c Release -r win-x64`; its DLLs are about twice the size, so the precompiled code is there.

| Build | Median | p90 | Install |
|---|---|---|---|
| JIT (Release build output) | 313 / 292 ms | 342 / 577 ms | 36.9 MB |
| **ReadyToRun (publish)** | **293 ms** (310 ms in an earlier run) | 380 ms | 18.7 MB |

**ReadyToRun makes no measurable difference.** The two .NET phases are the same in both builds: process start → `Main` ~55 ms, `Main` → window visible ~65 ms. So that time isn't JIT. It's runtime start-up and native window creation. The only gain is a smaller install: 18.7 MB published vs 36.9 MB of build output, which also carries the Linux and GirCore files.

**The whole app was about twice as fast as in the first session** (~300 ms vs ~600 ms). The difference is almost all in WebView2 controller creation: **~70–85 ms now vs ~335 ms before**. Our own phases didn't change. Other WebView2 processes from another app were running during this session, so the WebView2 runtime files were probably already in memory. That isn't proven. Until it is, treat ~300 ms as a warm-machine number and ~600 ms as a colder one, and compare variants only within one session. Navigate → first paint was ~80 ms in both sessions. Configure + navigate is now ~11 ms.

WebView2 memory at first paint was ~240 MB this session (~176 MB before).

## Next measurements

- **Why WebView2 controller creation varies 4×.** Measure with and without other WebView2 apps running (Teams, Outlook, Widgets), and after a reboot.
- **Per's WinUI 3 spike on the same PC.** Publish `spikes/SpikeWinUI` and run it with the same script, so both numbers come from one machine.
- **Native AOT.** `Microsoft.Web.WebView2.Core` uses built-in COM interop, which AOT doesn't support. That needs a ComWrappers-based binding first. Low priority: ReadyToRun showed the .NET phases aren't JIT-bound.
- **Linux (WebKitGTK).** Run the same script on a Linux machine.