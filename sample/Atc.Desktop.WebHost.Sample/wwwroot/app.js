// Sample page for Atc.Desktop.WebHost. Plain JavaScript, no build step.
// It talks to the host only through window.atcHost (defined by the host before this script runs).
"use strict";

const host = window.atcHost;
const $ = (id) => document.getElementById(id);
const epoch = () => performance.timeOrigin + performance.now();
const marks = { timeOrigin: performance.timeOrigin, scriptStart: epoch() };
let current = null;

function show(doc) {
  if (!doc) return;
  if (doc.error) {
    $("status").textContent = `Could not open ${doc.path}: ${doc.error}`;
    return;
  }

  if (current && current.path !== doc.path) host.request("file.unwatch", { path: current.path });
  current = doc;
  $("welcome").hidden = true;
  const text = $("text");
  text.hidden = false;
  text.textContent = doc.text;
  $("title").textContent = doc.name;
  host.request("window.setTitle", { title: `${doc.name} – Atc.Desktop.WebHost sample` });
  host.request("file.watch", { path: doc.path });
  $("status").textContent = `${doc.lines.toLocaleString()} lines · ${(doc.bytes / 1024).toFixed(1)} KB · read in ${doc.loadMs} ms`;
}

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
}

async function start() {
  const boot = host.boot;
  $("platform").textContent = boot.platform;

  let doc = boot.document;
  if (!doc && boot.documentPending) doc = await host.request("host.document");
  marks.documentReady = epoch();
  show(doc);
  marks.rendered = epoch();

  // First content paint: two frames after the content is in the DOM (the same rule as Per's spike).
  requestAnimationFrame(() => requestAnimationFrame(() => {
    marks.firstPaint = epoch();
    host.notify("host.firstPaint", { marks });
    if (!boot.bench) showStartup();
  }));
}

async function showStartup() {
  const info = await host.request("host.info");
  const m = info.marks;
  const parts = [`Started in ${Math.round(m.firstPaint ?? 0)} ms`];
  if (m.webviewStarting !== undefined && m.navigationStarted !== undefined) {
    parts.push(`webview ${Math.round(m.navigationStarted - m.webviewStarting)} ms`);
  }

  if (m.main !== undefined) parts.push(`.NET ${Math.round(m.main)} ms`);
  const status = $("status");
  status.textContent = current ? `${status.textContent} · ${parts.join(" · ")}` : parts.join(" · ");
  status.title = Object.entries(m).map(([k, v]) => `${k}: ${v} ms`).join("\n");
}

async function openFile() {
  const picked = await host.request("dialog.openFile", {});
  if (picked.path) show(await host.request("document.load", { path: picked.path }));
}

async function toggleTheme() {
  const next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
  await host.request("settings.set", { theme: next });
}

async function bridgeBench() {
  const panel = $("panel");
  panel.hidden = !panel.hidden;
  if (panel.hidden) return;
  const out = $("bridge-results");
  out.textContent = "Running…";
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

  out.innerHTML = "<table><tr><th>Test</th><th>median ms</th><th>p90 ms</th></tr>" +
    rows.map(([name, a, b]) => `<tr><td>${name}</td><td class="num">${a.toFixed(3)}</td><td class="num">${b.toFixed(3)}</td></tr>`).join("") +
    "</table><p>Events row: total ms, then ms per event.</p>";
}

function formatSize(bytes) {
  if (bytes === 0) return "empty";
  return bytes >= 1024 * 1024 ? `${bytes / 1024 / 1024} MB` : `${bytes / 1024} KB`;
}

host.on("settings.changed", (settings) => applyTheme(settings.theme === "dark" ? "dark" : "light"));
host.on("document.opened", show);
host.on("file.changed", async (change) => {
  if (!current || change.path.toLowerCase() !== current.path.toLowerCase() || change.kind !== "changed") return;
  const scroll = $("main").scrollTop;
  show(await host.request("document.load", { path: current.path }));
  $("main").scrollTop = scroll;
});

$("open").addEventListener("click", openFile);
$("theme").addEventListener("click", toggleTheme);
$("bench").addEventListener("click", bridgeBench);
document.addEventListener("keydown", (e) => {
  if (e.ctrlKey && e.key.toLowerCase() === "o") { e.preventDefault(); openFile(); }
});

host.ready();
start();