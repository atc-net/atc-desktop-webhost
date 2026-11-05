# 📜 Host contract

What a page can rely on when it runs inside `Atc.Desktop.WebHost`. Apps and the TypeScript package `@atc-net/viewer-host` build on this, so changes to it are breaking changes.

Status: **draft**. It describes what the host does today. Points marked 🟡 are proposed and not built yet.

## 1. The page's address

| | Windows (WebView2) | Linux (WebKitGTK) |
|---|---|---|
| App files (the frontend) | `https://app.atc-webhost.example/` | `atc-app://app/` |
| Mounts: extra folders, e.g. the document's images | `https://files.atc-webhost.example/<mount>/` | `atc-app://files/<mount>/` |

- `.example` is a reserved top-level domain, so the address can never be a real site.
- The frontend is served from a folder (fastest) or from embedded resources. Either way, the page never runs from `file://`, so `fetch` and ES modules work.
- Navigation away from the app, and links that would open a new window, open in the system browser instead (http, https and mailto only; other links are ignored).
- 🟡 **Proposed:** the same `https://` addresses on both platforms. On Linux this needs WebKitGTK to serve `https` for a custom host, which still has to be tried.

### Mounts

An app adds folders with `WebHostOptions.Mounts`, for example `new FrontendMount("doc", FrontendSource.FromFolder(folder))`. The page finds the address in `atcHost.boot.filesOrigin`: `${boot.filesOrigin}/doc/images/logo.png`.

- Mounts have their own origin because WebView2 serves a folder-mapped host entirely natively: a request handler is never called for it. The app's frontend keeps that fast path.
- Mounted files are read into memory for each request, so nothing stays locked. They are sent with `Cache-Control: no-cache` (change it with `FrontendMount.CacheControl`) and `Access-Control-Allow-Origin` for the app's origin, so the page can also `fetch` them.
- Paths with `..` segments, unknown mounts and missing files get 404.
- A mounted page can be shown in an iframe, but it is another origin: it talks to the app's page with `postMessage`, never to the host. Messages to the host are only accepted from the app's own page.

## 2. `window.atcHost`

The host injects this before any page script runs. It's the same on both platforms; pages never use `window.chrome.webview` or `window.webkit.messageHandlers` directly.

```ts
interface AtcHost {
  readonly platform: "webview2" | "webkitgtk";
  readonly boot: Boot;
  request<T>(type: string, payload?: unknown, options?: { signal?: AbortSignal }): Promise<T>;
  notify(type: string, payload?: unknown): void;               // no response
  on(type: string, handler: (payload: any, type: string) => void): () => void;  // "*" = every event
  ready(): void;                                                 // start receiving events
  getFilePaths(files: FileList | File[]): Promise<string[]>;     // full paths of dropped files (WebView2; [] on Linux)
}
```

- Only the app's page (the top frame) gets `atcHost` and the boot object. Iframes never do, so a mounted page or another site can't see the settings, file paths or document. Iframes talk to the app's page with `postMessage`.
- `ready()` is called automatically on `DOMContentLoaded`. Call it earlier if the page subscribes earlier.
- A failed request rejects with an `Error` that has a `code` (see section 5).
- Aborting the `signal` rejects the promise and tells the host to cancel the handler.

## 3. The boot object

Available as `atcHost.boot` before the first page script, so the first paint needs no round trip.

| Field | Type | Meaning |
|---|---|---|
| `appId` | string | The app's id, e.g. `AtcSoft.Markdown.Viewer` |
| `platform` | `"webview2"` \| `"webkitgtk"` | Same as `atcHost.platform` |
| `theme` | `"light"` \| `"dark"` | Resolved theme (the `theme` setting, or the OS when it's `system`). The window was painted in this theme's background colour |
| `origin` | string | The page's origin: `https://app.atc-webhost.example` on Windows, `atc-app://app` on Linux |
| `filesOrigin` | string | Where mounts are served: `https://files.atc-webhost.example` on Windows, `atc-app://files` on Linux |
| `bench` | boolean | Started with `--bench`: the page should report first paint and do nothing else |
| `files` | string[] | Files from the command line, full paths |
| `settings` | object | The app's settings (JSON object, app-defined) |
| `document` | any \| null | The first file, already loaded by the app's `LoadDocument`, when it was ready in time |
| `documentPending` | boolean | The first file is still loading: get it with `request("host.document")` |
| `hostMarks` | Record<string, number> | Host startup milestones, ms since process start |
| 🟡 `contractVersion` | number | Proposed: the version of this contract (starts at 1), so the TypeScript package can check it |

## 4. Messages

All messages are JSON text.

| Direction | Shape |
|---|---|
| page → host, request | `{ kind: "request", id, type, payload }` |
| page → host, notification | `{ kind: "request", type, payload }` (no `id`, no response) |
| page → host, cancel | `{ kind: "cancel", id }` |
| host → page, response | `{ kind: "response", id, ok: true, result }` or `{ kind: "response", id, ok: false, error: { code, message } }` |
| host → page, event | `{ kind: "event", type, payload }` |

- Events are held back until the page sends `host.ready`. Responses are never held back.
- The host only accepts messages from a page on its own origin.

### Built-in requests

| Type | Payload | Result |
|---|---|---|
| `host.ready` | – | `null`. Sent by `ready()` |
| `host.info` | – | `{ appId, platform, hostVersion, runtime, marks }` |
| `host.document` | – | The first document, or `null` if there is none. Waits until it's loaded |
| `host.firstPaint` | `{ marks: Record<string, number> }` (epoch ms) | `null`. Used for startup measurement; in bench mode the app then exits |
| `settings.get` | – | The settings object |
| `settings.set` | A partial settings object (merged) | The merged settings. Also sends `settings.changed` |
| `window.setTitle` | `{ title }` | `null` |
| `window.setCloseGuard` | `{ enabled }` | `null`. While enabled, the user's close sends `window.closeRequested` instead of closing (see "Unsaved work") |
| `window.close` | – | `null`. Closes the window, also when the close guard is on |
| `window.getMonitors` | – | `[{ number, bounds: { x, y, width, height }, workArea, isPrimary, scale }]`, numbered left to right, physical pixels |
| `window.setPlacement` | `{ monitor?, fullscreen?, showInTaskbar? }` | `null`. Moves the window; fields left out keep the window's current value (F11 changes it too). Leaving fullscreen brings the window back to its earlier size and place. Linux: only fullscreen on a display (Wayland can't place a normal window or hide it from the taskbar) |
| `page.find` | `{ text? }` | `null`. Opens the find bar (Windows). Linux has none: the page keeps Ctrl+F there |
| `page.print` | – | `null`. Opens the print dialog |
| `file.paths` | – | `string[]`. Sent by `getFilePaths` together with the files; without files the answer is `[]` |
| `window.closeAck` | – | `null`. Sent by the host script when `window.closeRequested` arrives; pages don't send it |
| `dialog.openFile` | `{ title?, defaultPath?, filters?: [{ name, patterns }] }` | `{ path }`, `path` is `null` when cancelled |
| `dialog.saveFile` | Same as `dialog.openFile` | `{ path }` |
| `document.load` | `{ path }` | The app's document, or `{ path, error }` if it couldn't be read |
| `file.watch` / `file.unwatch` | `{ path }` | `null` |
| `shell.openExternal` | `{ url }` (http, https, mailto) | `null` |

### Which files the page may use

`document.load` and `file.watch` only accept files in the folder of a file the user chose, or below it. The user chooses a file by passing it on the command line, opening it with a second launch, or picking it in the Open or Save dialog. Anything else is refused with `forbidden`, so a page, or script injected into it, can't read arbitrary files. Relative segments (`..`) are resolved before the check.

An app can allow more with `WebHostApplication.GrantFileAccess(path)`, for example for its own recent-files list, or turn the check off with `WebHostOptions.RestrictFileAccess = false` (trusted pages only).

### Built-in events

| Type | Payload | When |
|---|---|---|
| `settings.changed` | The merged settings | After `settings.set` |
| `document.opened` | The app's document | A second launch handed over a file |
| `file.changed` | `{ path, kind: "changed" \| "deleted" }` | A watched file changed. An atomic save (write to a temp file, rename over) is reported as `changed` |
| `window.closeRequested` | `null` | The user tried to close the window while the close guard is on |
| `window.placementChanged` | `{ monitor?, fullscreen, showInTaskbar }` | The window went into or out of fullscreen (F11, a video, the app) or moved to another display |

### Keys the host handles

These keys are handled before the page sees them (`WebHostOptions.Keys`, all on by default). An app can leave any out; a kiosk app should leave out F11.

| Key | What | Linux |
|---|---|---|
| F11 | Fullscreen without a frame. F11 again: back to the earlier size and place (a maximized window comes back maximized) | ✅ |
| Ctrl+F | The find bar | The page gets the key |
| Ctrl+P | The print dialog | ✅ |
| Ctrl+F4 | Close, like the title-bar button: with the close guard on, the page is asked first | ✅ |

When a page element goes fullscreen (a video's fullscreen button), the window goes fullscreen too and comes back when the element leaves. Whoever started fullscreen ends it: a video ending its fullscreen doesn't undo F11. Esc ends a video's fullscreen.

### Right-click menu

`WebHostOptions.ContextMenu`: `Edit` (default) shows cut, copy, paste, paste as plain text and select all, so text can be copied; `None` shows nothing; `Full` shows the webview's whole menu (always with `--devtools`).

### Dropped files

A dropped `File` in the page has no path. `atcHost.getFilePaths(e.dataTransfer.files)` asks the host: on Windows, WebView2 hands the file objects to the host, which reads their real paths, lets the page use them (like files from the Open dialog) and answers with the paths. The paths never travel in the message text, so a page can't make the host grant a path it invents. On Linux the answer is `[]` for now.

```js
document.addEventListener("dragover", (e) => e.preventDefault());
document.addEventListener("drop", async (e) => {
  e.preventDefault();
  const [path] = await atcHost.getFilePaths(e.dataTransfer.files);
  if (path) show(await atcHost.request("document.load", { path }));
});
```

### Iframes

An iframe may load web pages (http, https), the app's own origins and inline content (`about:`, `data:`, `blob:`). Other addresses (`file:`, `mailto:`, other programs' schemes) are refused. A link clicked to another site opens in the browser.

### Unsaved work (close guard)

A page with unsaved work calls `request("window.setCloseGuard", { enabled: true })`, and turns it off again when the work is saved. While it's on:

1. The user closes the window (title-bar button, Alt+F4, the taskbar). The window stays open and the page gets `window.closeRequested`.
2. The page asks its own question ("Save changes?") and, if the answer is to close, calls `request("window.close")`.

```js
atcHost.on("window.closeRequested", async () => {
  if (await askSaveDiscardOrCancel() !== "cancel") atcHost.request("window.close");
});
```

**The handler must not block the page.** Use an in-page dialog, not `confirm()` or `alert()`. While a native dialog is open the page's script is paused, so it can't acknowledge the user's next close attempt. The host then treats the page as hung and closes the window, with the unsaved work still in it. Also expect `window.closeRequested` again while your dialog is already open.

The window can always be closed, even when the page has hung:
- The host script answers `window.closeRequested` at once with `window.closeAck`. If no answer comes, the user's next close attempt after 3 seconds closes the window.
- A page that turned the guard on without listening for `window.closeRequested` is closed at once.
- A reload, a navigation within the app or a crashed web process turns the guard off.
- Closing from .NET (`IHostWindow.Close()`) always closes.

Apps add their own requests and events with `Bridge.Handle` and `Bridge.Emit`. Use a prefix per app (for example `md.render`) so names don't clash with the built-in ones.

## 5. Error codes

| Code | Meaning |
|---|---|
| `bad_request` | The message or payload is invalid |
| `unknown_type` | No handler for this request type |
| `cancelled` | The page cancelled the request |
| `forbidden` | The file is outside the folders the user opened (see "Which files the page may use") |
| `not_ready` | The window isn't created yet |
| `not_supported` | The app doesn't support this (for example `document.load` without a `LoadDocument`) |
| `handler_error` | The handler threw an unexpected exception |

Apps throw `BridgeException(code, message)` for their own expected errors.

## 6. Startup and the theme

- The window is painted in the theme's background colour before the webview exists: light `#FFFFFF`, dark `#0D1117` by default (`WebHostOptions`). The page's own background must match, or the window flashes.
- First paint is measured as two animation frames after the document is in the DOM. The page reports it with `notify("host.firstPaint", { marks })`.

## 7. Versioning

- Adding a request, event, boot field or optional payload field is not breaking.
- Removing or renaming any of them, or changing a type or meaning, is breaking: it needs a new contract version and a coordinated change in `@atc-net/viewer-host`.
