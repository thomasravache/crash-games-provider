// SPEC-0066 E2E-01: the wallet journey of SPEC-0047 against the published environment (ADR-0029), after a deploy on the
// VPS (G6). Its own configuration, apart from src/frontend/playwright.config.ts: no local web server (that one starts
// game-pipa on 5180), only the tests of this folder, and the base address of the public casino.
//
//   APOGEU_E2E_PUBLISHED=1 pnpm -C src/frontend exec playwright test --config ../../tests/deploy/e2e/playwright.config.ts
//
// Without APOGEU_E2E_PUBLISHED=1 the test is skipped (it never runs in the pull request). APOGEU_DEMO_SITE_URL overrides
// the address of the casino (default https://cassino.143-95-218-240.sslip.io).
import { defineConfig, devices } from "@playwright/test";

const publicCasinoUrl = "https://cassino.143-95-218-240.sslip.io";

const env =
  (
    globalThis as {
      process?: { env: Record<string, string | undefined> };
    }
  ).process?.env ?? {};
// The shared steps of tests/e2e/demo-site read the casino address from APOGEU_DEMO_SITE_URL; the workers inherit it.
env["APOGEU_DEMO_SITE_URL"] ??= publicCasinoUrl;

export default defineConfig({
  testDir: ".",
  outputDir: "../../../artifacts/e2e-published",
  fullyParallel: false,
  forbidOnly: true,
  // ADR-0009: a flaky test is a defect, never retried until it passes.
  retries: 0,
  workers: 1,
  reporter: "list",
  use: {
    baseURL: env["APOGEU_DEMO_SITE_URL"],
    trace: "retain-on-failure",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "webkit-mobile", use: { ...devices["iPhone 15"] } },
  ],
});
