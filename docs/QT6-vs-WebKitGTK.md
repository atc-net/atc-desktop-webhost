# 🐧 Qt6 vs WebKitGTK on Linux

Why the Linux window uses **GTK4 + WebKitGTK 6.0** (through GirCore) and not **Qt6 + QtWebEngine**, and when we would look at this again.

📌 Status: **WebKitGTK for now.** The choice can be changed later: `IHostPlatform` lets us add a second Linux backend next to the GTK one without changing apps or the page contract (`docs/host-contract.md`).

## 🔍 The real question: which browser engine

The toolkit around the window matters little. The engine inside it matters a lot:

| | GTK4 + WebKitGTK | Qt6 + QtWebEngine |
|---|---|---|
| 🌐 Browser engine | WebKit (like Safari, GNOME Web) | Chromium (like WebView2 on Windows) |
| 🔗 .NET bindings | GirCore (`GirCore.WebKit-6.0`, MIT), pure C#, generated from GObject introspection | None that are maintained and cover QtWebEngine. We would write a C++ shim |
| 🛠️ Native code we build | None. We call the system libraries | A C++ shim, built per distro and architecture |
| 💻 On the user's machine | Usually present on GNOME (Ubuntu, Fedora); `libgtk-4-1` + `libwebkitgtk-6.0-4` otherwise | Present on KDE; otherwise a large install. An AppImage would bundle Chromium (roughly 150–200 MB) |
| ⚙️ Processes | UI process + one web process + network process | Chromium's process model (browser, renderer, GPU, utility) |
| 📄 License | LGPL | LGPL / GPL (Qt), with Chromium's licenses |
| 👥 Used by | Tauri and Wry on Linux, Photino, GNOME Web | Qt and KDE apps, e.g. Falkon |

## 🟩 Qt6 + QtWebEngine

### ✅ Pros
- 🪟 **Same engine as Windows.** WebView2 is Chromium, so pages would look and behave the same on both systems: fonts, layout, Mermaid diagrams, CSS features.
- 🚀 **Mature engine.** Chromium is fast, gets security fixes quickly, and has good DevTools.
- 🎨 **Native on KDE.** Qt is KDE's toolkit, so dialogs and theming fit there.
- 📈 **Popular for cross-platform C++ desktop apps.**

### ❌ Cons
- 🚫 **No usable C# bindings.** The host would need its own C++ layer. We avoid that on purpose: one reason Photino wasn't used is its C++ core, which the team doesn't want to build or maintain.
- 📦 **Size.** QtWebEngine is big if it isn't already installed, which matters for an AppImage.
- ⏱️ **Startup is unknown.** Chromium starts several processes, and nobody has measured QtWebEngine with this host. Startup is the main goal of this repo, so we don't switch without numbers.
- 🧠 **Memory.** Chromium's process model usually uses more memory than WebKitGTK.
- 👣 **Not native on GNOME,** which is the default desktop on Ubuntu and Fedora.

## 🟦 GTK4 + WebKitGTK

### ✅ Pros
- 💜 **Pure C#.** GirCore wraps the system libraries; no native code to build or ship.
- 📦 **Small install.** The libraries come from the distro; a `.deb` lists them as dependencies.
- ✔️ **Already working.** Window, page from a custom URL scheme, bridge, startup report, self-test and CI (under `xvfb-run`) all pass.
- 🧭 **The usual choice for webview hosts on Linux** (Tauri, Wry, Photino).
- 👣 **Native on GNOME,** the default desktop on the big distros.

### ❌ Cons
- 🪟 **Different engine from Windows.** Pages can render differently (fonts, Mermaid, newer CSS), so we test on both.
- 🤔 **Mixed reputation.** Tauri users report slower rendering and engine bugs on Linux compared to Chromium.
- 🧪 **GirCore is a preview** (0.9.0-preview.1, pinned). Expect API changes; missing calls may need our own P/Invoke.
- 🕳️ **Gaps we've found so far:**
  - the origin is `atc-app://` instead of `https://`
  - a script-driven navigation to another site hangs
  - dropped files give no paths yet
  - dark mode is read from `GTK_THEME` or GNOME settings only, so KDE may not be detected (the desktop portal's colour-scheme setting would fix this)
- 📏 **Measured only in WSL so far,** with software rendering (~820 ms to first paint). A real Linux machine with a GPU still needs measuring.

## 🏆 Why WebKitGTK for now

1. 💜 **No C++.** It keeps the whole host in C#, which is a deliberate choice for this repo.
2. ✔️ **It works today** and passes the same self-test as Windows.
3. 📦 **Small install,** with the libraries from the distro.
4. 📊 **No evidence yet that Qt6 would be faster** or that WebKitGTK's differences hurt our apps.

## 🔄 When we would look again

Any of these would be a reason to try a Chromium backend on Linux:

- 🖼️ WebKitGTK renders our apps' pages clearly worse (Mermaid, fonts, large files, scrolling) on real machines.
- 🐌 GirCore stalls or misses calls we can't add ourselves.
- 🙋 Users need features only Chromium has.

💡 **If we do, we would first try CEF** (for example Xilium.CefGlue, which has .NET bindings), not Qt. It gives the same Chromium engine without a C++ toolkit. Either way it would be a new `IHostPlatform` next to the GTK one, and it would be measured with the same benchmark scripts, using both text fixtures, before any decision.

## 🧭 Next steps that help either way

- ⏱️ Measure startup on a real Linux machine with a GPU, on GNOME and KDE.
- 🧪 Run the sample's self-test there and note any rendering differences from Windows.
- 🌗 Read dark mode through the desktop portal, so KDE and other desktops are detected.
