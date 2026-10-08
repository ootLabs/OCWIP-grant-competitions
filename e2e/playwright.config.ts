import { defineConfig } from "@playwright/test";

/**
 * The whole process in a browser (T-100), against a stack that is already
 * running: docker compose up -d, with mail going to Mailpit, which the stack
 * starts and the backend sends to by default. The addresses are the local
 * defaults; CI sets the same ones.
 */
export default defineConfig({
  testDir: "tests",
  // One scenario that builds on itself: nothing to gain from parallel workers.
  workers: 1,
  fullyParallel: false,
  retries: 0,
  // The budget of the whole run (plan v1: under five minutes).
  timeout: 4 * 60_000,
  expect: { timeout: 15_000 },
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report" }]],
  outputDir: "test-results",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:3000",
    // CI installs Playwright's Chromium; a machine that cannot download it
    // runs the installed Chrome instead (E2E_BROWSER_CHANNEL=chrome).
    channel: process.env.E2E_BROWSER_CHANNEL || undefined,
    // The production compose file on a test machine (T-112): Caddy's own
    // certificate authority, and the test domain pointed at this machine.
    ignoreHTTPSErrors: process.env.E2E_IGNORE_HTTPS_ERRORS === "1",
    launchOptions: process.env.E2E_HOST_RULES ? { args: [`--host-resolver-rules=${process.env.E2E_HOST_RULES}`] } : {},
    locale: "pl-PL",
    timezoneId: "Europe/Warsaw",
    // The recording CI keeps when the run fails.
    video: "retain-on-failure",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
});
