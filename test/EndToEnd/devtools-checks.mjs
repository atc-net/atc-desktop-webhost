// Windows checks that need input the page can't make itself, driven over WebView2's DevTools protocol:
// a real file drop, and the user closing the window (WM_CLOSE) while the close guard is on.
// Usage: node devtools-checks.mjs <devtools-port> <app-pid> <file-to-drop>
// The app must run with --isolated and --browser-args=--remote-debugging-port=<port>. Needs Node 22+ (WebSocket).
import { execFileSync } from "node:child_process";
import { basename } from "node:path";

const [port, pidText, dropFile] = process.argv.slice(2);
const pid = Number(pidText);
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
let failures = 0;

function check(name, ok, detail = "") {
  console.log(`${ok ? "pass" : "fail"}  ${name}${detail ? ` – ${detail}` : ""}`);
  if (!ok) failures++;
}

function isRunning() {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

/** What the user's close does: taskkill without /F sends WM_CLOSE to the window. */
function userClose() {
  try {
    execFileSync("taskkill", ["/PID", String(pid)], { stdio: "ignore" });
  } catch {
    // taskkill reports an error when the window refuses to close; that's expected with the guard on.
  }
}

async function until(condition, ms) {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    if (await condition()) return true;
    await sleep(100);
  }

  return false;
}

// Connect to the app's page.
let page;
await until(async () => {
  try {
    page = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find((t) => t.type === "page");
    return Boolean(page);
  } catch {
    return false;
  }
}, 30000);
if (!page) {
  console.log(`fail  connect – no page on DevTools port ${port}`);
  process.exit(1);
}

const socket = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
let nextId = 0;
const pending = new Map();
socket.onmessage = (message) => {
  const data = JSON.parse(message.data);
  pending.get(data.id)?.(data);
  pending.delete(data.id);
};
const send = (method, params = {}) => new Promise((resolve) => {
  const id = ++nextId;
  pending.set(id, resolve);
  socket.send(JSON.stringify({ id, method, params }));
});
const evaluate = async (expression) => {
  const reply = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
  if (reply.result?.exceptionDetails) throw new Error(reply.result.exceptionDetails.exception?.description ?? "script error");
  return reply.result?.result?.value;
};

// The tour is loaded after the first paint.
check("page and tour loaded", await until(async () => (await evaluate("Boolean(window.atcHost && window.Tour)")) === true, 30000));

// --- A real file drop --------------------------------------------------------------------------------------------

const loadCode = `atcHost.request("document.load", { path: ${JSON.stringify(dropFile)} }).then(() => "ok", (e) => e.code)`;
check("before the drop the file is forbidden", (await evaluate(loadCode)) === "forbidden");

await evaluate(`window.__dropped = new Promise((resolve) => document.addEventListener("drop",
  (e) => atcHost.getFilePaths(e.dataTransfer.files).then(resolve), { capture: true, once: true })); 1`);
const data = { items: [], files: [dropFile], dragOperationsMask: 1 };
for (const type of ["dragEnter", "dragOver", "drop"]) await send("Input.dispatchDragEvent", { type, x: 200, y: 200, data });
const dropped = await evaluate("window.__dropped");
check("getFilePaths gives the dropped file's path", JSON.stringify(dropped) === JSON.stringify([dropFile]), JSON.stringify(dropped));
check("after the drop the file loads", (await evaluate(loadCode)) === "ok");
check("the tour shows the dropped file",
  await until(async () => (await evaluate(`document.getElementById("title").textContent`)) === basename(dropFile), 5000));

// --- The user's close with the close guard on --------------------------------------------------------------------

await evaluate(`window.__closeRequests = 0; atcHost.on("window.closeRequested", () => window.__closeRequests++);
  atcHost.request("window.setCloseGuard", { enabled: true }).then(() => 1)`);
userClose();
check("guard on: the user's close asks the page", await until(async () => (await evaluate("window.__closeRequests")) === 1, 5000));
check("guard on: the window stays open", isRunning());
check("guard on: the page shows its dialog", (await evaluate(`!document.getElementById("close-dialog").hidden`)) === true);

userClose();
check("guard on: a second close asks again", await until(async () => (await evaluate("window.__closeRequests")) === 2, 5000));
check("guard on: still open", isRunning());

// The dialog's Close button sends window.close, which closes even with the guard on.
await evaluate(`document.getElementById("close-confirm").click(); 1`).catch(() => {});
check("window.close closes the app", await until(() => !isRunning(), 10000));

socket.close();
console.log(failures === 0 ? "DEVTOOLS=pass" : `DEVTOOLS=fail (${failures})`);
process.exit(failures === 0 ? 0 : 1);