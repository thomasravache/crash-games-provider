// Playwright configuration (ADR-0009). The E2E tests live in tests/e2e, outside the pnpm workspace; they resolve
// @playwright/test and @axe-core/playwright from src/frontend/node_modules through the `paths` of their tsconfig.
import { defineConfig, devices } from "@playwright/test";

// Fixed and strict, so a test never runs against another server that took the port.
const gamePipaPort = 5180;
const baseURL = `http://127.0.0.1:${String(gamePipaPort)}`;
const isCi = process.env["CI"] !== undefined;

export default defineConfig({
  testDir: "../../tests/e2e",
  // Traces of failed tests; artifacts/ is ignored by git.
  outputDir: "artifacts/e2e",
  fullyParallel: true,
  forbidOnly: isCi,
  // ADR-0009: a flaky test is a defect, never retried until it passes.
  retries: 0,
  reporter: "list",
  use: {
    baseURL,
    trace: "retain-on-failure",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "webkit-mobile", use: { ...devices["iPhone 15"] } },
  ],
  webServer: {
    command: `pnpm --filter @apogeu/game-pipa exec vite --host 127.0.0.1 --port ${String(gamePipaPort)} --strictPort`,
    url: baseURL,
    reuseExistingServer: !isCi,
    timeout: 60_000,
  },
});
