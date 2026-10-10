// Playwright configuration (ADR-0009): Chromium, WebKit mobile and Firefox. The E2E tests live in tests/e2e, outside
// the pnpm workspace; they resolve @playwright/test and @axe-core/playwright from src/frontend/node_modules through the
// `paths` of their tsconfig.
import { defineConfig, devices } from "@playwright/test";

// Fixed and strict, so a test never runs against another server that took the port.
const gamePipaPort = 5180;
const baseURL = `http://127.0.0.1:${String(gamePipaPort)}`;

// Visual, WebGL and performance tests of the scene (SPEC-0011): Chromium only.
const sceneTests = "pipa-visual/**";

// WebGL through ANGLE on SwiftShader (software GL): the same renderer headless or headed, on every runner, so the
// captures of the visual regression do not depend on a GPU.
const chromiumWebGl = [
  "--use-angle=swiftshader",
  "--enable-unsafe-swiftshader",
  "--ignore-gpu-blocklist",
];

export default defineConfig({
  testDir: "../../tests/e2e",
  // Traces of failed tests, in the ignored artifacts/ of the repository root (outside lint and format scope).
  outputDir: "../../artifacts/e2e",
  fullyParallel: true,
  // A focused test (test.only) never reaches a commit: it would silently skip the rest of the suite.
  forbidOnly: true,
  // ADR-0009: a flaky test is a defect, never retried until it passes.
  retries: 0,
  reporter: "list",
  // SPEC-0011 E2E-01: reference images of the scene keyframes, one per test file, project and platform. They are
  // generated on the Linux runner of the CI (where they are compared), approved by the user and committed; a missing
  // reference fails the test instead of being written (`--update-snapshots` creates them on purpose).
  snapshotPathTemplate:
    "{testDir}/{testFileDir}/__screenshots__/{testFileName}/{arg}-{projectName}-{platform}{ext}",
  updateSnapshots: "none",
  expect: {
    // SPEC-0011 E2E-01: at most 1% of the pixels may differ from the approved reference.
    toHaveScreenshot: { maxDiffPixelRatio: 0.01 },
  },
  use: {
    baseURL,
    trace: "retain-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], launchOptions: { args: chromiumWebGl } },
    },
    { name: "webkit-mobile", use: { ...devices["iPhone 15"] }, testIgnore: sceneTests },
    { name: "firefox", use: { ...devices["Desktop Firefox"] }, testIgnore: sceneTests },
  ],
  webServer: {
    command: `pnpm --filter @apogeu/game-pipa exec vite --host 127.0.0.1 --port ${String(gamePipaPort)} --strictPort`,
    url: baseURL,
    // Always a fresh server of the current code; the strict port fails fast if another process holds it.
    reuseExistingServer: false,
    timeout: 60_000,
  },
});
