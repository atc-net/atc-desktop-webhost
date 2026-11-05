// Sample page for Atc.Desktop.WebHost. Plain JavaScript, no build step.
// It talks to the host only through window.atcHost (defined by the host before this script runs).
// This file shows the document and measures startup; the tour (tour.js) loads after the first paint,
// and not at all in bench mode, so the startup numbers measure the host, not the tour.
"use strict";

const host = window.atcHost;
const texts = window.SampleTexts;
const $ = (id) => document.getElementById(id);
const epoch = () => performance.timeOrigin + performance.now();
const marks = { timeOrigin: performance.timeOrigin, scriptStart: epoch() };
let current = null;
let startupText = "";

function show(doc) {
  if (!doc) return;
  if (doc.error) {
    $("status").textContent = texts.t("doc.error", { path: doc.path, error: doc.error });
    return;
  }

  if (current && current.path !== doc.path) host.request("file.unwatch", { path: current.path });
  current = doc;
  $("welcome").hidden = true;
  const text = $("text");
  text.hidden = false;
  text.textContent = doc.text;
  $("title").removeAttribute("data-i18n");
  $("title").textContent = doc.name;
  host.request("window.setTitle", { title: `${doc.name} – Atc.Desktop.WebHost sample` });
  host.request("file.watch", { path: doc.path });
  showStatus();
}

function showStatus() {
  const parts = [];
  if (current && !current.error) {
    parts.push(texts.t("doc.stats", { lines: current.lines.toLocaleString(texts.language), kb: (current.bytes / 1024).toFixed(1), ms: current.loadMs }));
  }

  if (startupText) parts.push(startupText);
  if (parts.length) $("status").textContent = parts.join(" · ");
}

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
}

async function start() {
  const boot = host.boot;
  $("platform").textContent = boot.platform;
  texts.setLanguage(boot.language);
  $("language").value = boot.language;

  let doc = boot.document;
  if (!doc && boot.documentPending) doc = await host.request("host.document");
  marks.documentReady = epoch();
  show(doc);
  marks.rendered = epoch();

  // First content paint: two frames after the content is in the DOM (the same rule as Per's spike).
  requestAnimationFrame(() => requestAnimationFrame(() => {
    marks.firstPaint = epoch();
    host.notify("host.firstPaint", { marks });
    if (boot.bench) return;
    showStartup();
    loadScript("tour.js").then(() => window.Tour.start({ show, current: () => current }));
  }));
}

function loadScript(src) {
  return new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = src;
    script.onload = resolve;
    script.onerror = () => reject(new Error(`Could not load ${src}`));
    document.head.append(script);
  });
}

async function showStartup() {
  const info = await host.request("host.info");
  const m = info.marks;
  const parts = [texts.t("startup.started", { ms: Math.round(m.firstPaint ?? 0) })];
  if (m.webviewStarting !== undefined && m.navigationStarted !== undefined) {
    parts.push(`webview ${Math.round(m.navigationStarted - m.webviewStarting)} ms`);
  }

  if (m.main !== undefined) parts.push(`.NET ${Math.round(m.main)} ms`);
  startupText = parts.join(" · ");
  showStatus();
  $("status").title = Object.entries(m).map(([k, v]) => `${k}: ${v} ms`).join("\n");
}

async function openFile() {
  const picked = await host.request("dialog.openFile", {});
  if (picked.path) show(await host.request("document.load", { path: picked.path }));
}

async function toggleTheme() {
  const next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
  await host.request("settings.set", { theme: next });
}

host.on("settings.changed", (settings) => {
  applyTheme(settings.theme === "dark" || settings.theme === "light" ? settings.theme : host.boot.theme);
  const language = settings.language ?? host.boot.language;
  if (language !== $("language").value) {
    $("language").value = language;
    texts.setLanguage(language);
  }
});
host.on("document.opened", show);
host.on("file.changed", async (change) => {
  if (!current || change.path.toLowerCase() !== current.path.toLowerCase() || change.kind !== "changed") return;
  const scroll = $("main").scrollTop;
  show(await host.request("document.load", { path: current.path }));
  $("main").scrollTop = scroll;
});
texts.onLanguage(showStatus);

$("open").addEventListener("click", openFile);
$("theme").addEventListener("click", toggleTheme);
$("language").addEventListener("change", (e) => host.request("settings.set", { language: e.target.value }));
document.addEventListener("keydown", (e) => {
  if (e.ctrlKey && e.key.toLowerCase() === "o") { e.preventDefault(); openFile(); }
});

host.ready();
start();