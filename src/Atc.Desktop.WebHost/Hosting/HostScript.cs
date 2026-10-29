namespace Atc.Desktop.WebHost.Hosting;

/// <summary>
/// The script injected before any page script. It defines <c>window.atcHost</c>, the same small API on
/// every platform, so the page (and <c>@atc-net/viewer-host</c>) never touches
/// <c>window.chrome.webview</c> or <c>window.webkit.messageHandlers</c> directly.
/// <code>
/// atcHost.platform                       // "webview2" | "webkitgtk"
/// atcHost.boot                           // { appId, theme, settings, document, documentPending, hostMarks, ... }
/// atcHost.request(type, payload, { signal }) → Promise
/// atcHost.notify(type, payload)          // no response
/// atcHost.on(type, (payload) => ...) → unsubscribe
/// atcHost.ready()                        // start receiving events (also called on DOMContentLoaded)
/// </code>
/// </summary>
internal static class HostScript
{
    /// <summary>The WebKitGTK script message handler name (<c>window.webkit.messageHandlers.atcHost</c>).</summary>
    public const string MessageHandlerName = "atcHost";

    /// <summary>The global function the Linux host calls to deliver a message.</summary>
    public const string ReceiveFunctionName = "__atcHostReceive";

    private const string Shim = """
        (() => {
          "use strict";
          if (window.atcHost) return;
          const boot = window.__ATC_BOOT__;
          const pending = new Map();
          const listeners = new Map();
          let nextId = 1;
          let isReady = false;
          const send = boot.platform === "webview2"
            ? (m) => window.chrome.webview.postMessage(m)
            : (m) => window.webkit.messageHandlers.atcHost.postMessage(m);
          const receive = (m) => {
            if (typeof m === "string") m = JSON.parse(m);
            if (m.kind === "response") {
              const p = pending.get(m.id);
              if (!p) return 0;
              pending.delete(m.id);
              if (m.ok) p.resolve(m.result);
              else p.reject(Object.assign(new Error(m.error.message), { code: m.error.code }));
            } else if (m.kind === "event") {
              for (const key of [m.type, "*"]) {
                const set = listeners.get(key);
                if (set) for (const f of [...set]) { try { f(m.payload, m.type); } catch (e) { console.error(e); } }
              }
            }
            return 0;
          };
          if (boot.platform === "webview2") window.chrome.webview.addEventListener("message", (e) => receive(e.data));
          Object.defineProperty(window, "__atcHostReceive", { value: receive });
          const notify = (type, payload) => send(JSON.stringify({ kind: "request", type, payload }));
          const request = (type, payload, options) => new Promise((resolve, reject) => {
            const id = nextId++;
            const signal = options && options.signal;
            if (signal) {
              if (signal.aborted) { reject(signal.reason); return; }
              signal.addEventListener("abort", () => {
                if (pending.delete(id)) { send(JSON.stringify({ kind: "cancel", id })); reject(signal.reason); }
              }, { once: true });
            }
            pending.set(id, { resolve, reject });
            send(JSON.stringify({ kind: "request", id, type, payload }));
          });
          const on = (type, handler) => {
            let set = listeners.get(type);
            if (!set) listeners.set(type, (set = new Set()));
            set.add(handler);
            return () => set.delete(handler);
          };
          const ready = () => { if (!isReady) { isReady = true; notify("host.ready"); } };
          document.addEventListener("DOMContentLoaded", ready, { once: true });
          window.atcHost = Object.freeze({ platform: boot.platform, boot, request, notify, on, ready });
        })();
        """;

    /// <summary>Builds the full script: the boot data followed by the shim.</summary>
    /// <param name="bootJson">The boot object as JSON.</param>
    /// <returns>The JavaScript source.</returns>
    public static string Build(string bootJson)
        => string.Concat("window.__ATC_BOOT__=", bootJson, ";\n", Shim);

    /// <summary>Builds a script that delivers one message (used where the webview has no post-message API).</summary>
    /// <param name="messageJson">The JSON message.</param>
    /// <returns>The JavaScript source.</returns>
    public static string BuildReceiveCall(string messageJson)
        => string.Concat(ReceiveFunctionName, "(", messageJson, ")");
}