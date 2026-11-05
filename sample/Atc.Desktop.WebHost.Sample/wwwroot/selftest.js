// In-page self-test: checks the host contract (docs/host-contract.md) through the real window, webview and bridge.
// Run from the Self-test tab, or with --selftest: then the host writes a report and the app exits with 1 on a failure.
// Left out because they need a person: the Open, Save and Open Folder dialogs, print, the find bar, opening a browser, keys, and a
// user's close (test/EndToEnd/devtools-checks.mjs covers drops and closes on Windows).
"use strict";

window.SelfTest = (() => {
  const host = window.atcHost;
  const isWindows = host.platform === "webview2";
  const tests = [];
  let files;

  class Skip extends Error {}

  const test = (name, fn, options = {}) => tests.push({ name, fn, ...options });
  const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
  const samePath = (a, b) => (isWindows ? a.toLowerCase() === b.toLowerCase() : a === b);

  function assert(condition, message) {
    if (!condition) throw new Error(message);
  }

  /** JSON with object keys sorted: the host may send keys in another order. */
  function canonical(value) {
    return JSON.stringify(value, (_, v) => (v && typeof v === "object" && !Array.isArray(v)
      ? Object.fromEntries(Object.keys(v).sort().map((k) => [k, v[k]]))
      : v));
  }

  function equal(actual, expected, what) {
    const a = canonical(actual);
    const e = canonical(expected);
    if (a !== e) throw new Error(`${what}: expected ${e}, got ${a}`);
  }

  /** The error code a request fails with, or "ok". */
  async function errorCode(promise) {
    try {
      await promise;
      return "ok";
    } catch (e) {
      return e.code ?? `${e.name}: ${e.message}`;
    }
  }

  function within(promise, ms, what) {
    let timer;
    const timeout = new Promise((_, reject) => { timer = setTimeout(() => reject(new Error(`${what}: nothing within ${ms} ms`)), ms); });
    return Promise.race([promise, timeout]).finally(() => clearTimeout(timer));
  }

  /** The next event of a type that passes the filter. Subscribe before causing it. */
  function nextEvent(type, ms, filter = () => true) {
    let off;
    const event = new Promise((resolve) => {
      off = host.on(type, (payload) => { if (filter(payload)) resolve(payload); });
    });
    return within(event, ms, `${type} event`).finally(() => off());
  }

  async function until(condition, ms, what) {
    const end = performance.now() + ms;
    while (!condition()) {
      if (performance.now() > end) throw new Error(`${what}: not within ${ms} ms`);
      await sleep(20);
    }
  }

  function frameMessage(src, ms) {
    const frame = document.createElement("iframe");
    frame.style.cssText = "position:absolute;width:1px;height:1px;opacity:0;border:0";
    let listener;
    const message = new Promise((resolve) => {
      listener = (e) => { if (e.source === frame.contentWindow) resolve(e.data); };
      window.addEventListener("message", listener);
    });
    frame.src = src;
    document.body.append(frame);
    return within(message, ms, "message from the frame").finally(() => {
      window.removeEventListener("message", listener);
      frame.remove();
    });
  }

  // --- Boot and host -------------------------------------------------------------------------------------------

  test("boot object", () => {
    const boot = host.boot;
    assert(["webview2", "webkitgtk"].includes(boot.platform), `platform ${boot.platform}`);
    equal(boot.platform, host.platform, "boot.platform");
    equal(boot.origin, `${location.protocol}//${location.host}`, "boot.origin");
    assert(typeof boot.filesOrigin === "string" && boot.filesOrigin !== boot.origin, `filesOrigin ${boot.filesOrigin}`);
    assert(["en", "da-DK", "de-DE"].includes(boot.language), `language ${boot.language}`);
    assert(["light", "dark"].includes(boot.theme), `theme ${boot.theme}`);
    assert(Array.isArray(boot.files), "files is an array");
    assert(boot.settings && typeof boot.settings === "object", "settings is an object");
    assert(boot.hostMarks && boot.hostMarks.main >= 0, "hostMarks.main");
    return `language ${boot.language}, theme ${boot.theme}`;
  });

  test("host.info", async () => {
    const info = await host.request("host.info");
    equal(info.appId, host.boot.appId, "appId");
    equal(info.platform, host.platform, "platform");
    assert(/^\d+\.\d+/.test(info.hostVersion), `hostVersion ${info.hostVersion}`);
    assert(/^\d+\.\d+/.test(info.runtime), `runtime ${info.runtime}`);
    assert(info.marks.firstPaint > 0, "marks.firstPaint");
  });

  test("host.document", async () => {
    const doc = await host.request("host.document");
    if (host.boot.files.length === 0) {
      equal(doc, null, "document without a file");
      return;
    }

    assert(doc && samePath(doc.path, host.boot.files[0]), `document path ${doc?.path}`);
    assert(typeof doc.text === "string" && doc.text.length > 0, "document text");
  });

  // --- Bridge ----------------------------------------------------------------------------------------------------

  test("request keeps text and structure", async () => {
    const value = { text: "æøå ÆØÅ ß € 😀 \"quoted\" </script> \\  ", number: 1.5, list: [1, null, true, "x"], nested: { empty: [] } };
    equal(await host.request("sample.echo", value), value, "echo");
  });

  test("request with 1 MB", async () => {
    const data = "x".repeat(1024 * 1024);
    const result = await host.request("sample.echo", { data });
    equal(result.data.length, data.length, "length");
  });

  test("events arrive in order", async () => {
    const received = [];
    const off = host.on("sample.burstItem", (item) => received.push(item.index));
    try {
      await host.request("sample.burst", { count: 200, size: 16 });
      await until(() => received.length >= 200, 5000, "200 events");
    } finally {
      off();
    }

    equal(received, [...Array(200).keys()], "event order");
  });

  test("unknown request: unknown_type", async () => {
    equal(await errorCode(host.request("selftest.noSuchRequest")), "unknown_type", "code");
  });

  test("invalid payload: bad_request", async () => {
    equal(await errorCode(host.request("window.setTitle", 42)), "bad_request", "window.setTitle(42)");
    equal(await errorCode(host.request("settings.set", "text")), "bad_request", "settings.set(text)");
  });

  test("abort cancels the handler", async () => {
    const controller = new AbortController();
    const request = host.request("sample.delay", { ms: 10000 }, { signal: controller.signal });
    await sleep(50);
    controller.abort();
    const code = await errorCode(request);
    assert(code !== "ok", "the request was not rejected");
    let state;
    for (let i = 0; i < 30 && !state?.cancelled; i++) {
      state = await host.request("sample.delayState");
      if (!state.cancelled) await sleep(100);
    }

    assert(state?.cancelled, "the host's handler was not cancelled");
  });

  test("shell.openExternal refuses other schemes", async () => {
    equal(await errorCode(host.request("shell.openExternal", { url: "file:///etc/hosts" })), "bad_request", "file: link");
  });

  // --- Settings and language -------------------------------------------------------------------------------------

  test("settings.set, settings.changed, settings.get", async () => {
    const value = Date.now();
    const changed = nextEvent("settings.changed", 3000, (s) => s.selfTestValue === value);
    const merged = await host.request("settings.set", { selfTestValue: value });
    equal(merged.selfTestValue, value, "merged settings");
    await changed;
    equal((await host.request("settings.get")).selfTestValue, value, "settings.get");
    await host.request("settings.set", { selfTestValue: null });
  });

  test("language setting", async () => {
    const before = (await host.request("settings.get")).language ?? null;
    const other = host.boot.language === "de-DE" ? "da-DK" : "de-DE";
    try {
      const changed = nextEvent("settings.changed", 3000, (s) => s.language === other);
      await host.request("settings.set", { language: other });
      await changed;
    } finally {
      await host.request("settings.set", { language: before });
    }
  });

  // --- Window ----------------------------------------------------------------------------------------------------

  test("window.setTitle", async () => {
    equal(await host.request("window.setTitle", { title: document.title }), null, "result");
  });

  test("window.getMonitors", async () => {
    const monitors = await host.request("window.getMonitors");
    assert(monitors.length >= 1, "at least one display");
    equal(monitors.filter((m) => m.isPrimary).length, 1, "primary displays");
    equal(monitors.map((m) => m.number), monitors.map((_, i) => i + 1), "numbers");
    for (const m of monitors) assert(m.bounds.width > 0 && m.bounds.height > 0 && m.scale > 0, `display ${m.number} size`);
  });

  test("fullscreen and back", async () => {
    const width = innerWidth;
    const height = innerHeight;
    try {
      const entered = nextEvent("window.placementChanged", 5000, (p) => p.fullscreen === true);
      await host.request("window.setPlacement", { fullscreen: true });
      await entered;
      const left = nextEvent("window.placementChanged", 5000, (p) => p.fullscreen === false);
      await host.request("window.setPlacement", { fullscreen: false });
      await left;
    } catch (e) {
      // A display server without a window manager (xvfb in CI) never confirms fullscreen.
      if (!isWindows) throw new Skip(`${e.message} (no window manager?)`);
      throw e;
    }

    await until(() => Math.abs(innerWidth - width) <= 2 && Math.abs(innerHeight - height) <= 2, 3000,
      `back to ${width} × ${height} (now ${innerWidth} × ${innerHeight})`);
  });

  test("close guard on and off", async () => {
    equal(await host.request("window.setCloseGuard", { enabled: true }), null, "on");
    equal(await host.request("window.setCloseGuard", { enabled: false }), null, "off");
  });

  // --- Files -----------------------------------------------------------------------------------------------------

  test("document.load in an allowed folder", async () => {
    const doc = await host.request("document.load", { path: files.watchedFile });
    assert(samePath(doc.path, files.watchedFile), `path ${doc.path}`);
    assert(doc.text.startsWith("self-test file"), "text");
  });

  test("document.load outside: forbidden", async () => {
    equal(await errorCode(host.request("document.load", { path: files.outsideFile })), "forbidden", "outside file");
    equal(await errorCode(host.request("document.load", { path: files.traversalPath })), "forbidden", "path with ..");
  });

  test("file.watch reports a change", async () => {
    await host.request("file.watch", { path: files.watchedFile });
    try {
      const changed = nextEvent("file.changed", 5000, (c) => samePath(c.path, files.watchedFile));
      await host.request("selftest.touch");
      equal((await changed).kind, "changed", "kind");
    } finally {
      await host.request("file.unwatch", { path: files.watchedFile });
    }
  });

  test("file.watch outside: forbidden", async () => {
    equal(await errorCode(host.request("file.watch", { path: files.outsideFile })), "forbidden", "code");
  });

  // --- Folders ---------------------------------------------------------------------------------------------------

  const listed = (result) => result.files.map((f) => f.relativePath);

  test("folder.list: *.md with subfolders, any case, no dot folders", async () => {
    const result = await host.request("folder.list", { path: files.wikiFolder, pattern: "*.md", recursive: true });
    equal(listed(result), ["Home.md", "Sub/Page.MD"], "files");
    equal(result.truncated, false, "truncated");
    const home = result.files[0];
    assert(samePath(home.path, `${files.wikiFolder}${isWindows ? "\\" : "/"}Home.md`), `path ${home.path}`);
    assert(home.size > 0 && !Number.isNaN(Date.parse(home.modified)), `size ${home.size}, modified ${home.modified}`);
  });

  test("folder.list: one level, and a cut list", async () => {
    equal(listed(await host.request("folder.list", { path: files.wikiFolder, pattern: "*.md" })), ["Home.md"], "one level");
    const cut = await host.request("folder.list", { path: files.wikiFolder, recursive: true, maxFiles: 1 });
    equal([cut.files.length, cut.truncated], [1, true], "maxFiles 1");
  });

  test("folder.list errors: forbidden, bad_request, not_found", async () => {
    const parent = files.wikiFolder.replace(/[\\/][^\\/]+$/, "");
    const sep = isWindows ? "\\" : "/";
    equal(await errorCode(host.request("folder.list", { path: parent })), "forbidden", "parent folder");
    equal(await errorCode(host.request("folder.list", { path: `${files.wikiFolder}${sep}..` })), "forbidden", "path with ..");
    equal(await errorCode(host.request("folder.list", { path: files.wikiFolder, pattern: "../*.md" })), "bad_request", "pattern with ..");
    equal(await errorCode(host.request("folder.list", { path: `${files.wikiFolder}${sep}missing` })), "not_found", "missing folder");
  });

  test("folder.watch reports a new file in a subfolder", async () => {
    await host.request("folder.watch", { path: files.wikiFolder, recursive: true });
    try {
      const changed = nextEvent("folder.changed", 5000, (c) => samePath(c.folder, files.wikiFolder));
      const added = await host.request("selftest.addPage");
      const event = await changed;
      assert(event.paths.some((p) => samePath(p, added)), `paths ${JSON.stringify(event.paths)}`);
    } finally {
      await host.request("folder.unwatch", { path: files.wikiFolder });
    }
  });

  test("access.list remembers the command-line file", async () => {
    if (host.boot.files.length === 0) throw new Skip("started without a file");
    const list = await host.request("access.list");
    assert(list.some((e) => samePath(e.path, host.boot.files[0]) && e.isFolder === false && !Number.isNaN(Date.parse(e.used))),
      `list ${JSON.stringify(list)}`);
    equal(await errorCode(host.request("access.forget", { path: "" })), "bad_request", "empty path");
  });

  test("folder.watch outside: forbidden", async () => {
    const parent = files.wikiFolder.replace(/[\\/][^\\/]+$/, "");
    equal(await errorCode(host.request("folder.watch", { path: parent, recursive: true })), "forbidden", "code");
  });

  test("file.paths without files is empty", async () => {
    equal(await host.request("file.paths"), [], "paths");
  });

  test("getFilePaths: a file made in the page has no path", async () => {
    const paths = await within(host.getFilePaths([new File(["x"], "made-in-page.txt")]), 3000, "getFilePaths");
    assert(Array.isArray(paths) && paths.every((p) => !p), `paths ${JSON.stringify(paths)}`);
  });

  // --- Origins, mounts and frames --------------------------------------------------------------------------------

  const mount = () => `${host.boot.filesOrigin}/tour`;

  test("app files load", async () => {
    const response = await fetch("app.css");
    equal(response.status, 200, "status");
  });

  test("mount: fetch a file", async () => {
    const response = await fetch(`${mount()}/data.json`);
    equal(response.status, 200, "status");
    equal((await response.json()).name, "tour", "content");
    equal(response.headers.get("cache-control"), "no-cache", "Cache-Control");
  });

  test("mount: missing file and unknown mount are 404", async () => {
    equal((await fetch(`${mount()}/missing.txt`)).status, 404, "missing file");
    equal((await fetch(`${host.boot.filesOrigin}/no-such-mount/data.json`)).status, 404, "unknown mount");
  });

  test("mount: image", async () => {
    const image = new Image();
    const loaded = new Promise((resolve, reject) => { image.onload = resolve; image.onerror = () => reject(new Error("the image failed")); });
    image.src = `${mount()}/logo.svg`;
    await within(loaded, 5000, "image");
    assert(image.naturalWidth > 0, "image size");
  });

  test("iframe from the mount: no atcHost, postMessage works", async () => {
    const message = await frameMessage(`${mount()}/frame.html`, 5000);
    equal(message.kind, "tour-frame", "message");
    equal(message.hasAtcHost, false, "atcHost in the frame");
  });

  test("iframe may load inline content", async () => {
    const html = "<script>parent.postMessage({ kind: 'inline' }, '*')<\/script>";
    equal((await frameMessage(`data:text/html,${encodeURIComponent(html)}`, 5000)).kind, "inline", "message");
  });

  /** Runs every check in order; onResult gets each result as it finishes. A check may return a note for the report. */
  async function run(onResult) {
    files = await host.request("selftest.files");
    const results = [];
    for (const t of tests) {
      const started = performance.now();
      let status = "pass";
      let message = null;
      try {
        if (t.only && t.only !== host.platform) throw new Skip(`${t.only} only`);
        const note = await within(Promise.resolve().then(t.fn), t.timeout ?? 15000, t.name);
        if (typeof note === "string") message = note;
      } catch (e) {
        status = e instanceof Skip ? "skip" : "fail";
        message = String(e?.message ?? e);
      }

      const result = { name: t.name, status, ms: Math.round(performance.now() - started), message };
      results.push(result);
      onResult?.(result);
    }

    return results;
  }

  return { run };
})();