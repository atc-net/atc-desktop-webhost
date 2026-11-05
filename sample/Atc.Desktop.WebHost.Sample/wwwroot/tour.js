// The feature tour: one tab per part of the host. Loaded by app.js after the first paint.
// Each tab is built the first time it's opened. Started with --selftest, the page runs the self-test at once.
"use strict";

window.Tour = (() => {
  const host = window.atcHost;
  const texts = window.SampleTexts;
  const { t } = texts;
  const $ = (id) => document.getElementById(id);
  const isLinux = host.platform === "webkitgtk";
  const built = new Set();
  let app;

  /** Creates an element: el("button", { "data-i18n": "x", onclick }, child, ...). */
  function el(tag, attrs = {}, ...children) {
    const node = document.createElement(tag);
    for (const [name, value] of Object.entries(attrs)) {
      if (name.startsWith("on")) node.addEventListener(name.slice(2), value);
      else if (value !== undefined && value !== null && value !== false) node.setAttribute(name, value === true ? "" : value);
    }

    node.append(...children.filter((c) => c !== null && c !== undefined));
    return node;
  }

  const heading = (key) => el("h2", { "data-i18n": key });
  const para = (key, cls) => el("p", { "data-i18n": key, class: cls });

  function showTab(name) {
    for (const button of $("tabs").querySelectorAll("button")) button.classList.toggle("active", button.dataset.tab === name);
    for (const section of document.querySelectorAll("main > .tab")) section.hidden = section.id !== `tab-${name}`;
    if (!built.has(name)) {
      built.add(name);
      builders[name]?.($(`tab-${name}`));
      texts.apply($(`tab-${name}`));
    }
  }

  // --- Document: dropped files ---------------------------------------------------------------------------------

  function dropFiles() {
    document.addEventListener("dragover", (e) => e.preventDefault());
    document.addEventListener("drop", async (e) => {
      e.preventDefault();
      const [path] = await host.getFilePaths(e.dataTransfer.files);
      if (!path) return;
      showTab("document");
      app.show(await host.request("document.load", { path }));
    });
  }

  // --- Window ----------------------------------------------------------------------------------------------------

  function buildWindow(root) {
    const displays = el("table");
    const fullscreen = el("input", { type: "checkbox", id: "fullscreen" });
    const taskbar = el("input", { type: "checkbox", id: "taskbar", checked: true, disabled: isLinux });
    const log = el("ol", { class: "log" }, el("li", { "data-i18n": "window.noEvents" }));

    root.append(
      heading("window.displays"), displays,
      heading("window.placement"),
      el("div", { class: "row" },
        el("label", {}, fullscreen, " ", el("span", { "data-i18n": "window.fullscreen" })),
        el("label", {}, taskbar, " ", el("span", { "data-i18n": "window.taskbar" }))),
      isLinux ? para("window.linuxPlacement", "hint") : null,
      heading("window.pageTools"),
      el("div", { class: "row" },
        el("button", { type: "button", "data-i18n": "window.find", onclick: () => host.request("page.find", {}) }),
        el("button", { type: "button", "data-i18n": "window.print", onclick: () => host.request("page.print") })),
      heading("window.keysTitle"), para("window.keys"),
      heading("window.events"), log);

    fullscreen.addEventListener("change", () => host.request("window.setPlacement", { fullscreen: fullscreen.checked }));
    taskbar.addEventListener("change", () => host.request("window.setPlacement", { showInTaskbar: taskbar.checked }));

    host.on("window.placementChanged", (p) => {
      fullscreen.checked = p.fullscreen;
      taskbar.checked = p.showInTaskbar;
      if (log.firstElementChild?.dataset.i18n) log.replaceChildren();
      log.prepend(el("li", {}, `${new Date().toLocaleTimeString()} · ${JSON.stringify(p)}`));
    });

    renderDisplays(displays);
  }

  async function renderDisplays(table) {
    const monitors = await host.request("window.getMonitors");
    const row = (m) => el("tr", {},
      el("td", {}, el("span", { "data-display": m.number })),
      el("td", {}, `${m.bounds.width} × ${m.bounds.height} @ ${m.bounds.x},${m.bounds.y}`),
      el("td", {}, `${Math.round(m.scale * 100)} %`),
      el("td", {}, m.isPrimary ? el("span", { "data-i18n": "window.primary" }) : ""),
      el("td", {}, el("button", { type: "button", "data-i18n": "window.moveTo", onclick: () => host.request("window.setPlacement", { monitor: m.number }) })));
    table.replaceChildren(...monitors.map(row));
    texts.apply(table);
    const label = () => { for (const s of table.querySelectorAll("[data-display]")) s.textContent = t("window.display", { n: s.dataset.display }); };
    label();
    texts.onLanguage(label);
  }

  // --- Close guard (in the window tab, but always active) --------------------------------------------------------

  function closeGuard() {
    const dialog = $("close-dialog");
    host.on("window.closeRequested", () => {
      // Expect this again while the dialog is already open: keep the one dialog.
      dialog.hidden = false;
      $("close-confirm").focus();
    });
    $("close-cancel").addEventListener("click", () => { dialog.hidden = true; });
    $("close-confirm").addEventListener("click", () => host.request("window.close"));
  }

  function buildGuardSection(root) {
    const guard = el("input", { type: "checkbox", id: "guard" });
    guard.addEventListener("change", () => host.request("window.setCloseGuard", { enabled: guard.checked }));
    root.append(
      heading("window.guardTitle"),
      el("label", {}, guard, " ", el("span", { "data-i18n": "window.guard" })),
      para("window.guardHint", "hint"));
  }

  // --- Files and frames ------------------------------------------------------------------------------------------

  function buildFiles(root) {
    const mount = `${host.boot.filesOrigin}/tour`;
    const mountText = el("p", { class: "hint" });
    const fetched = el("pre", { class: "box" }, "…");
    const frameSays = el("p", { class: "hint" }, "…");
    const frame = el("iframe", { class: "frame", src: `${mount}/frame.html`, title: "Mounted frame" });

    root.append(
      heading("files.dropTitle"), para("files.drop"),
      heading("files.mountTitle"), mountText,
      el("div", { class: "row" }, el("img", { class: "mount-image", src: `${mount}/logo.svg`, alt: "" }), el("code", {}, `${mount}/logo.svg`)),
      fetched,
      heading("files.frameTitle"), para("files.frame", "hint"), frame, frameSays);

    let frameText = "";
    const fill = () => {
      mountText.textContent = t("files.mount", { origin: host.boot.filesOrigin });
      if (frameText) frameSays.textContent = t("files.frameSays", { text: frameText });
    };
    texts.onLanguage(fill);
    fill();

    window.addEventListener("message", (e) => {
      if (e.source !== frame.contentWindow || e.data?.kind !== "tour-frame") return;
      frameText = `${e.origin} · atcHost: ${e.data.hasAtcHost ? "yes" : "no"}`;
      fill();
    });

    fetch(`${mount}/data.json`).then(async (r) => {
      fetched.textContent = `GET ${mount}/data.json → ${r.status}\nCache-Control: ${r.headers.get("cache-control")}\n\n${await r.text()}`;
    }, (e) => { fetched.textContent = String(e); });
  }

  // --- Bridge ----------------------------------------------------------------------------------------------------

  function buildBridge(root) {
    const out = el("div");
    const run = el("button", { type: "button", "data-i18n": "bridge.run" });
    run.addEventListener("click", async () => {
      run.disabled = true;
      out.replaceChildren(el("p", { "data-i18n": "bridge.running" }));
      texts.apply(out);
      try { out.replaceChildren(await bridgeBench()); texts.apply(out); } finally { run.disabled = false; }
    });
    root.append(heading("bridge.title"), para("bridge.hint", "hint"), el("div", { class: "row" }, run), out);
  }

  async function bridgeBench() {
    const rows = [];
    for (const size of [0, 1024, 100 * 1024, 1024 * 1024]) {
      const payload = { data: "x".repeat(size) };
      const runs = size >= 1024 * 1024 ? 20 : 200;
      for (let i = 0; i < 5; i++) await host.request("sample.echo", payload);
      const times = [];
      for (let i = 0; i < runs; i++) {
        const t0 = performance.now();
        await host.request("sample.echo", payload);
        times.push(performance.now() - t0);
      }

      times.sort((a, b) => a - b);
      rows.push([`echo ${formatSize(size)}`, times[Math.floor(times.length / 2)], times[Math.floor(times.length * 0.9)]]);
    }

    const count = 1000;
    let received = 0;
    const t0 = performance.now();
    const done = new Promise((resolve) => {
      const off = host.on("sample.burstItem", () => { if (++received === count) { off(); resolve(); } });
    });
    await host.request("sample.burst", { count, size: 1024 });
    await done;
    const burstMs = performance.now() - t0;
    rows.push([`${count} events × 1 KB`, burstMs, burstMs / count]);

    return el("div", {},
      el("table", {},
        el("tr", {}, el("th", { "data-i18n": "bridge.test" }), el("th", {}, "median ms"), el("th", {}, "p90 ms")),
        ...rows.map(([name, a, b]) => el("tr", {}, el("td", {}, name), el("td", { class: "num" }, a.toFixed(3)), el("td", { class: "num" }, b.toFixed(3))))),
      para("bridge.events", "hint"));
  }

  function formatSize(bytes) {
    if (bytes === 0) return "empty";
    return bytes >= 1024 * 1024 ? `${bytes / 1024 / 1024} MB` : `${bytes / 1024} KB`;
  }

  // --- Self-test -------------------------------------------------------------------------------------------------

  let selfTestLoaded;
  const loadSelfTest = () => (selfTestLoaded ??= new Promise((resolve, reject) => {
    const script = el("script", { src: "selftest.js" });
    script.onload = resolve;
    script.onerror = () => reject(new Error("Could not load selftest.js"));
    document.head.append(script);
  }));

  let runSelfTest;

  function buildSelfTest(root) {
    const summary = el("p");
    const table = el("table");
    const run = el("button", { type: "button", "data-i18n": "self.run" });
    let counts = null;
    const fillSummary = () => { summary.textContent = counts ? t("self.summary", counts) : ""; };
    texts.onLanguage(fillSummary);

    runSelfTest = async () => {
      run.disabled = true;
      table.replaceChildren();
      counts = { passed: 0, failed: 0, skipped: 0 };
      fillSummary();
      try {
        await loadSelfTest();
        const results = await window.SelfTest.run((r) => {
          counts[{ pass: "passed", fail: "failed", skip: "skipped" }[r.status]]++;
          fillSummary();
          table.append(el("tr", {},
            el("td", { class: r.status }, r.status),
            el("td", {}, r.name),
            el("td", { class: "num" }, `${r.ms} ms`),
            el("td", { class: "hint" }, r.message ?? "")));
        });

        // The self-test turns the close guard off; put back what the checkbox says.
        const guard = $("guard");
        if (guard?.checked) await host.request("window.setCloseGuard", { enabled: true });
        return results;
      } finally {
        run.disabled = false;
      }
    };

    run.addEventListener("click", () => runSelfTest());
    root.append(heading("self.title"), para("self.hint", "hint"), el("div", { class: "row" }, run), summary, table);
  }

  const builders = {
    window: (root) => { buildWindow(root); buildGuardSection(root); },
    files: buildFiles,
    bridge: buildBridge,
    selftest: buildSelfTest,
  };

  async function start(context) {
    app = context;
    for (const button of $("tabs").querySelectorAll("button")) button.addEventListener("click", () => showTab(button.dataset.tab));
    dropFiles();
    closeGuard();

    const mode = await host.request("sample.mode");
    if (mode.selfTest) {
      showTab("selftest");
      let results;
      try {
        results = await runSelfTest();
      } catch (e) {
        results = [{ name: "self-test page", status: "fail", ms: 0, message: String(e?.message ?? e) }];
      }

      await host.request("selftest.done", { results });
    }
  }

  return { start };
})();