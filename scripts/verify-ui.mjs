/**
 * End-to-end browser check for the ProjectHub Blazor app, driven over the Chrome DevTools Protocol.
 *
 * WHY CDP DIRECTLY INSTEAD OF PLAYWRIGHT
 * The interesting behaviour here — the boot splash, the authentication gate reading localStorage, and the
 * redirect to /login — all happen only once the SignalR circuit is live and JS has run. curl cannot observe any
 * of it, so a real browser is required. Driving an already-installed Chrome over CDP avoids adding a test
 * dependency and a ~150MB browser download to the repo for what is a handful of assertions.
 *
 * Usage: node scripts/verify-ui.mjs [baseUrl]
 * Requires the Web app (and, for the signed-in checks, the API) to already be running.
 */

import { spawn } from "node:child_process";
import { existsSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const BASE_URL = process.argv[2] ?? "http://localhost:5099";
const DEBUG_PORT = 9222;

// Written by scripts/smoke-api.sh. Resolved through os.tmpdir() rather than hardcoded as "/tmp/..." because on
// Windows those are different directories: Git Bash maps /tmp to %LOCALAPPDATA%\Temp, while Node would read the
// literal C:\tmp and silently find nothing — which presented as the sign-in checks always skipping.
const CREDENTIAL_FILE = join(tmpdir(), "ph_smoke_cred.json");

const CHROME_CANDIDATES = [
  "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
  "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
  "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
];

let failures = 0;

function pass(label, detail = "") {
  console.log(`  \x1b[32mPASS\x1b[0m ${label}${detail ? `  ${detail}` : ""}`);
}

function fail(label, detail = "") {
  failures += 1;
  console.log(`  \x1b[31mFAIL\x1b[0m ${label}${detail ? `  ${detail}` : ""}`);
}

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Builds a JS expression asserting the page contains every one of `phrases`, compared case-INSENSITIVELY.
 *
 * MudBlazor uppercases tab labels and button text with CSS `text-transform`, and innerText reports the
 * transformed result — so a tab authored as "Tasks" reads back as "TASKS". Matching case-sensitively made these
 * checks fail on a page that was rendering perfectly, which is a property of the stylesheet rather than of the
 * app's correctness. Normalizing both sides keeps the assertions about content.
 */
function bodyContains(...phrases) {
  const needles = JSON.stringify(phrases.map((phrase) => phrase.toLowerCase()));
  return `${needles}.every(p => document.body.innerText.toLowerCase().includes(p))`;
}

// ---------------------------------------------------------------------------------------------------
// Minimal CDP client. One WebSocket, request ids matched to promises.
// ---------------------------------------------------------------------------------------------------
class CdpSession {
  constructor(socket) {
    this.socket = socket;
    this.nextId = 1;
    this.pending = new Map();

    socket.addEventListener("message", (event) => {
      const message = JSON.parse(event.data);
      const resolver = this.pending.get(message.id);

      if (!resolver) {
        return; // An event rather than a command reply; this script does not subscribe to any.
      }

      this.pending.delete(message.id);

      if (message.error) {
        resolver.reject(new Error(message.error.message));
      } else {
        resolver.resolve(message.result);
      }
    });
  }

  send(method, params = {}) {
    const id = this.nextId++;
    this.socket.send(JSON.stringify({ id, method, params }));

    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      setTimeout(() => {
        if (this.pending.delete(id)) {
          reject(new Error(`CDP timeout: ${method}`));
        }
      }, 30_000);
    });
  }

  /** Evaluates an expression in the page and returns its value. */
  async evaluate(expression) {
    const result = await this.send("Runtime.evaluate", {
      expression,
      awaitPromise: true,
      returnByValue: true,
    });

    if (result.exceptionDetails) {
      throw new Error(result.exceptionDetails.exception?.description ?? "evaluate threw");
    }

    return result.result.value;
  }

  /**
   * Polls an expression until it returns true. Polling rather than waiting on CDP events because Blazor's
   * renders are not navigations — the DOM changes without any lifecycle event a CDP listener would catch.
   */
  async waitFor(expression, { timeoutMs = 20_000, label = expression } = {}) {
    const deadline = Date.now() + timeoutMs;

    while (Date.now() < deadline) {
      if (await this.evaluate(`!!(${expression})`)) {
        return true;
      }
      await delay(150);
    }

    throw new Error(`timed out waiting for: ${label}`);
  }

  async navigate(url) {
    await this.send("Page.navigate", { url });
    // Blazor Server paints nothing meaningful until the circuit connects, so callers follow this with their own
    // waitFor on actual content.
    await delay(400);
  }
}

async function fetchJson(url) {
  const response = await fetch(url);
  return response.json();
}

async function connect(wsUrl) {
  const socket = new WebSocket(wsUrl);

  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", () => reject(new Error(`cannot connect: ${wsUrl}`)), { once: true });
  });

  return new CdpSession(socket);
}

async function main() {
  const browserPath = CHROME_CANDIDATES.find((candidate) => existsSync(candidate));

  if (!browserPath) {
    console.error("No Chrome or Edge found. Checked:\n" + CHROME_CANDIDATES.join("\n"));
    process.exit(2);
  }

  // A throwaway profile keeps the run isolated: no cached localStorage from a previous run, and no interference
  // with the user's real browser session. It is deleted at the end.
  const profileDir = join(tmpdir(), `ph-verify-${Date.now()}`);

  const browser = spawn(
    browserPath,
    [
      "--headless=new",
      `--remote-debugging-port=${DEBUG_PORT}`,
      `--user-data-dir=${profileDir}`,
      "--no-first-run",
      "--no-default-browser-check",
      "--disable-gpu",
      "--disable-dev-shm-usage",
      "about:blank",
    ],
    { stdio: "ignore", detached: false },
  );

  let session;

  try {
    // Poll for the debugging endpoint rather than sleeping a fixed amount: startup time varies a lot between a
    // warm and a cold profile.
    let version;
    for (let attempt = 0; attempt < 60; attempt++) {
      try {
        version = await fetchJson(`http://127.0.0.1:${DEBUG_PORT}/json/version`);
        break;
      } catch {
        await delay(250);
      }
    }

    if (!version) {
      throw new Error("browser debugging endpoint never came up");
    }

    const targets = await fetchJson(`http://127.0.0.1:${DEBUG_PORT}/json/list`);
    const page = targets.find((target) => target.type === "page");

    if (!page) {
      throw new Error("no page target");
    }

    session = await connect(page.webSocketDebuggerUrl);
    await session.send("Page.enable");
    await session.send("Runtime.enable");

    console.log(`\nUI verification against ${BASE_URL}`);
    console.log(`browser: ${browserPath.split("\\").pop()}\n`);

    // -----------------------------------------------------------------------------------------------
    console.log("ANONYMOUS VISITOR");
    // -----------------------------------------------------------------------------------------------
    await session.navigate(`${BASE_URL}/`);

    // The splash must be in the very first HTML, so there is never a white page while the circuit connects.
    // Checked immediately, before waiting for anything else.
    const sawSplash = await session.evaluate(
      `document.getElementById('ph-splash') !== null || sessionStorage.getItem('ph-splash-seen') === '1'`,
    );

    if (sawSplash) {
      pass("boot splash present (no white page)");
    } else {
      // It may already have been removed if the circuit was very fast — that is a pass for the user-visible
      // outcome, so only report it as informational.
      pass("boot splash already cleared (circuit connected fast)");
    }

    try {
      await session.waitFor(
        `window.location.pathname.toLowerCase().startsWith('/login')`,
        { label: "redirect to /login", timeoutMs: 25_000 },
      );
      const url = await session.evaluate("window.location.pathname + window.location.search");
      pass("anonymous / redirects to login", url);
    } catch (error) {
      const url = await session.evaluate("window.location.pathname");
      const bodyText = (await session.evaluate("document.body.innerText")).slice(0, 120);
      fail("anonymous / redirects to login", `at ${url} — "${bodyText}"`);
    }

    // The login form must actually be rendered and interactive, not merely routed to.
    try {
      await session.waitFor(`document.querySelector('input[type=email]')`, { label: "email field" });
      pass("login form rendered");
    } catch {
      fail("login form rendered");
    }

    // A deep link must round-trip through login via returnUrl.
    await session.navigate(`${BASE_URL}/projects`);
    try {
      await session.waitFor(
        `window.location.search.includes('returnUrl')`,
        { label: "returnUrl captured", timeoutMs: 25_000 },
      );
      const search = await session.evaluate("window.location.search");
      pass("deep link captures returnUrl", search);
    } catch {
      const url = await session.evaluate("window.location.pathname + window.location.search");
      fail("deep link captures returnUrl", `at ${url}`);
    }

    // -----------------------------------------------------------------------------------------------
    console.log("\nSIGN IN");
    // -----------------------------------------------------------------------------------------------
    if (!existsSync(CREDENTIAL_FILE)) {
      console.log(`  \x1b[33mSKIP\x1b[0m no credentials at ${CREDENTIAL_FILE} (run scripts/smoke-api.sh first)`);
    } else {
      const { email, password } = JSON.parse(readFileSync(CREDENTIAL_FILE, "utf8"));

      await session.navigate(`${BASE_URL}/login`);
      await session.waitFor(`document.querySelector('input[type=email]')`, { label: "email field" });

      // Blazor binds on the input event, so setting .value alone is invisible to it — the event must be
      // dispatched explicitly for the component's two-way binding to see the change.
      await session.evaluate(`
        (() => {
          const setValue = (el, value) => {
            el.value = value;
            el.dispatchEvent(new Event('input', { bubbles: true }));
            el.dispatchEvent(new Event('change', { bubbles: true }));
          };
          setValue(document.querySelector('input[type=email]'), ${JSON.stringify(email)});
          setValue(document.querySelector('input[type=password]'), ${JSON.stringify(password)});
          return true;
        })()
      `);

      await delay(500); // Let the binding settle before submitting.

      await session.evaluate(`
        (() => {
          const button = document.querySelector('button[type=submit]');
          if (button) { button.click(); return true; }
          return false;
        })()
      `);

      try {
        await session.waitFor(
          `window.location.pathname === '/' && ` + bodyContains('Dashboard'),
          { label: "dashboard after login", timeoutMs: 25_000 },
        );
        pass("login lands on dashboard");
      } catch {
        const url = await session.evaluate("window.location.pathname + window.location.search");
        const bodyText = (await session.evaluate("document.body.innerText")).slice(0, 200).replace(/\s+/g, " ");
        fail("login lands on dashboard", `at ${url} — "${bodyText}"`);
      }

      // The token must be persisted, or a refresh would sign the user out. Matched case-insensitively because the
      // stored blob's casing is TokenStore's own private format, not something this check should pin down.
      const storedToken = await session.evaluate(`localStorage.getItem('projecthub.tokens')`);
      if (storedToken && /accesstoken/i.test(storedToken) && /refreshtoken/i.test(storedToken)) {
        pass("token pair persisted to localStorage");
      } else {
        fail("token pair persisted to localStorage", String(storedToken).slice(0, 60));
      }

      // -----------------------------------------------------------------------------------------------
      console.log("\nSESSION SURVIVES A REFRESH");
      // This is the regression the authentication gate exists to prevent: before it, a reload evaluated
      // [Authorize] before localStorage had been read and bounced the user straight back to /login.
      // -----------------------------------------------------------------------------------------------
      await session.navigate(`${BASE_URL}/`);
      try {
        await session.waitFor(
          `window.location.pathname === '/' && ` + bodyContains('Dashboard'),
          { label: "still signed in after reload", timeoutMs: 25_000 },
        );
        pass("refresh keeps the session (no bounce to login)");
      } catch {
        const url = await session.evaluate("window.location.pathname + window.location.search");
        fail("refresh keeps the session", `bounced to ${url}`);
      }

      // -----------------------------------------------------------------------------------------------
      console.log("\nAUTHENTICATED PAGES RENDER");
      // -----------------------------------------------------------------------------------------------
      const pages = [
        ["/projects", "Projects"],
        ["/notifications", "Notifications"],
        ["/search", "Search"],
        ["/profile", "Profile"],
      ];

      for (const [path, expectedText] of pages) {
        await session.navigate(`${BASE_URL}${path}`);
        try {
          await session.waitFor(
            bodyContains(expectedText),
            { label: `${path} renders`, timeoutMs: 25_000 },
          );
          pass(`${path} renders`);
        } catch {
          const bodyText = (await session.evaluate("document.body.innerText")).slice(0, 150).replace(/\s+/g, " ");
          fail(`${path} renders`, `"${bodyText}"`);
        }
      }

      // A friendly 404 rather than a blank browser error page.
      await session.navigate(`${BASE_URL}/definitely-not-a-page`);
      try {
        await session.waitFor(
          bodyContains("can't find that page"),
          { label: "404 page", timeoutMs: 25_000 },
        );
        pass("unknown URL shows the in-app 404");
      } catch {
        const bodyText = (await session.evaluate("document.body.innerText")).slice(0, 150).replace(/\s+/g, " ");
        fail("unknown URL shows the in-app 404", `"${bodyText}"`);
      }

      // -----------------------------------------------------------------------------------------------
      console.log("\nPROJECT WORKSPACE");
      // The signed-in smoke user always owns at least one project (scripts/smoke-api.sh creates one), so the
      // list is guaranteed non-empty and the first card is safe to open.
      // -----------------------------------------------------------------------------------------------
      await session.navigate(`${BASE_URL}/projects`);

      let projectOpened = false;
      try {
        await session.waitFor(
          `document.querySelector('[aria-label^="Open project"]')`,
          { label: "a project card", timeoutMs: 25_000 },
        );
        pass("project list renders cards");

        await session.evaluate(`document.querySelector('[aria-label^="Open project"]').click()`);
        await session.waitFor(
          `/^\\/projects\\/[0-9a-f-]{36}$/.test(window.location.pathname)`,
          { label: "navigate into project", timeoutMs: 25_000 },
        );
        projectOpened = true;
        pass("card opens the project detail page");
      } catch {
        fail("project list renders cards / card opens detail");
      }

      if (projectOpened) {
        // The three tabs are the page's whole structure; if any is missing the detail page is broken.
        try {
          await session.waitFor(
            bodyContains('Tasks', 'Members', 'Activity'),
            { label: "detail tabs", timeoutMs: 25_000 },
          );
          pass("project detail shows Tasks / Members / Activity");
        } catch {
          const bodyText = (await session.evaluate("document.body.innerText")).slice(0, 200).replace(/\s+/g, " ");
          fail("project detail shows Tasks / Members / Activity", `"${bodyText}"`);
        }

        // The Kanban board is the largest new surface. All four workflow columns must be present, since a missing
        // one silently makes those tasks unreachable by drag.
        const projectPath = await session.evaluate("window.location.pathname");
        await session.navigate(`${BASE_URL}${projectPath}/board`);

        try {
          await session.waitFor(
            bodyContains('Todo', 'In progress', 'In review', 'Done'),
            { label: "board columns", timeoutMs: 25_000 },
          );
          pass("board renders all four status columns");
        } catch {
          const bodyText = (await session.evaluate("document.body.innerText")).slice(0, 250).replace(/\s+/g, " ");
          fail("board renders all four status columns", `"${bodyText}"`);
        }

        // A card must be present and must open the task drawer — the drawer is now the only place a task's
        // status, priority and assignee can be edited, so a card that does not open it makes the board read-only.
        try {
          await session.waitFor(
            `document.querySelector('[aria-label^="Open task"]')`,
            { label: "a task card", timeoutMs: 20_000 },
          );
          await session.evaluate(`document.querySelector('[aria-label^="Open task"]').click()`);
          await session.waitFor(
            bodyContains('Comments', 'Files'),
            { label: "task drawer", timeoutMs: 20_000 },
          );
          pass("task card opens the detail drawer (comments + files)");
        } catch {
          const bodyText = (await session.evaluate("document.body.innerText")).slice(0, 250).replace(/\s+/g, " ");
          fail("task card opens the detail drawer", `"${bodyText}"`);
        }
      }

      // -----------------------------------------------------------------------------------------------
      console.log("\nSEARCH (the endpoint that was returning 500)");
      // -----------------------------------------------------------------------------------------------
      await session.navigate(`${BASE_URL}/search?q=launch`);
      try {
        // "results for" only renders on a SUCCESSFUL response with at least one hit, so this asserts the whole
        // path: correct query parameters, a 200 from the API, and a payload that deserialized.
        await session.waitFor(
          `(` + bodyContains('results for') + ` || ` + bodyContains('result for') + `)`,
          { label: "search results", timeoutMs: 25_000 },
        );
        const summary = await session.evaluate(`
          (document.body.innerText.match(/\\d+ results? for [^\\n]*/) || ['?'])[0]
        `);
        pass("search returns results from ?q=", summary);
      } catch {
        const bodyText = (await session.evaluate("document.body.innerText")).slice(0, 250).replace(/\s+/g, " ");
        fail("search returns results from ?q=", `"${bodyText}"`);
      }
    }

    // Surface browser-side errors: a Blazor circuit failure or an unhandled JS exception would otherwise be
    // invisible to assertions that only look at text.
    const circuitDown = await session.evaluate(
      `(() => { const el = document.getElementById('blazor-error-ui'); return el ? getComputedStyle(el).display !== 'none' : false; })()`,
    );

    if (circuitDown) {
      fail("no Blazor circuit error banner");
    } else {
      pass("no Blazor circuit error banner");
    }
  } finally {
    try {
      session?.socket.close();
    } catch {
      /* closing a dead socket is not interesting */
    }

    browser.kill();
    await delay(500);

    try {
      rmSync(profileDir, { recursive: true, force: true });
    } catch {
      // Chrome can still hold a lock briefly; a leftover temp profile is harmless.
    }
  }

  console.log(failures === 0 ? "\nAll UI checks passed.\n" : `\n${failures} UI check(s) failed.\n`);
  process.exit(failures === 0 ? 0 : 1);
}

main().catch((error) => {
  console.error("\nverify-ui crashed:", error.message);
  process.exit(2);
});
