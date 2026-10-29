# 📜 Host contract

What a page can rely on when it runs inside `Atc.Desktop.WebHost`. Apps and the TypeScript package `@atc-net/viewer-host` build on this, so changes to it are breaking changes.

Status: **draft**. It describes what the host does today. Points marked 🟡 are proposed and not built yet.

## 1. The page's address

| | Windows (WebView2) | Linux (WebKitGTK) |
|---|---|---|
| App files (the frontend) | `https://app.atc-webhost.example/` | `atc-app://app/` |
| Files next to the document (images) | 🟡 not served yet | 🟡 not served yet |

- `.example` is a reserved top-level domain, so the address can never be a real site.
- The frontend is served from a folder (fastest) or from embedded resources. Either way, the page never runs from `file://`, so `fetch` and ES modules work.
- Navigation away from the app opens the system browser (http, https, mailto only). On Linux this isn't done yet: links navigate inside the app.
- 🟡 **Known gap:** on Linux, `boot.origin` reports the Windows address, not `atc-app://app`.
- 🟡 **Proposed:** the same `https://` addresses on both platforms, with a second host for the document's folder (for example `https://doc.atc-webhost.example/`). On Linux this needs WebKitGTK to serve `https` for a custom host, which still has to be tried.

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
}
```

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
| `origin` | string | The page's origin (see the Linux gap above) |
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
- On Windows the host only accepts messages from its own origin.
- 🟡 **Known gap:** on Linux the host doesn't check the sender yet. Because links still navigate inside the app there, a page from another site could send requests. Both need fixing before an app ships on Linux.

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
| `dialog.openFile` | `{ title?, defaultPath?, filters?: [{ name, patterns }] }` | `{ path }`, `path` is `null` when cancelled |
| `dialog.saveFile` | Same as `dialog.openFile` | `{ path }` |
| `document.load` | `{ path }` | The app's document, or `{ path, error }` if it couldn't be read |
| `file.watch` / `file.unwatch` | `{ path }` | `null` |
| `shell.openExternal` | `{ url }` (http, https, mailto) | `null` |

🟡 `document.load` and `file.watch` accept any path today. Proposed: only the open document's folder and paths the user picked in a dialog or passed on the command line.

### Built-in events

| Type | Payload | When |
|---|---|---|
| `settings.changed` | The merged settings | After `settings.set` |
| `document.opened` | The app's document | A second launch handed over a file |
| `file.changed` | `{ path, kind: "changed" \| "deleted" }` | A watched file changed. An atomic save (write to a temp file, rename over) is reported as `changed` |

Apps add their own requests and events with `Bridge.Handle` and `Bridge.Emit`. Use a prefix per app (for example `md.render`) so names don't clash with the built-in ones.

## 5. Error codes

| Code | Meaning |
|---|---|
| `bad_request` | The message or payload is invalid |
| `unknown_type` | No handler for this request type |
| `cancelled` | The page cancelled the request |
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
