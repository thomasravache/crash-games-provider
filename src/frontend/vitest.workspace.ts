// Vitest projects of the front-end (ADR-0009). Vitest 5 has no workspace file discovery, so the package.json scripts pass
// this file with --config. The tests live in tests/frontend (outside the pnpm workspace) and run from it as project root.
import { defineConfig } from "vitest/config";

const testsRoot = "../../tests/frontend";
// Keeps the Vite/Vitest cache in src/frontend/node_modules instead of creating one under tests/frontend.
const cacheDir = "../../src/frontend/node_modules/.vite";

// Absolute path of a file next to this config (URL of the DOM lib: the config has no Node types).
const source = (path: string): string =>
  decodeURIComponent(new URL(path, import.meta.url).pathname);

// tests/frontend sits outside the workspace, so the workspace packages resolve through these aliases (same entries
// as the `exports` of each package.json; the most specific first). `@apogeu/game-pipa/*` reaches the app sources.
const alias = [
  { find: "@apogeu/game-sdk/state", replacement: source("./packages/game-sdk/src/state/index.ts") },
  {
    find: "@apogeu/game-sdk/fairness",
    replacement: source("./packages/game-sdk/src/fairness/index.ts"),
  },
  { find: /^@apogeu\/game-sdk$/, replacement: source("./packages/game-sdk/src/index.ts") },
  { find: /^@apogeu\/scene-kit$/, replacement: source("./packages/scene-kit/src/index.ts") },
  { find: /^@apogeu\/game-ui$/, replacement: source("./packages/game-ui/src/index.ts") },
  { find: /^@apogeu\/contracts$/, replacement: source("./packages/contracts/src/index.ts") },
  { find: /^@apogeu\/game-pipa\/(.*)$/, replacement: `${source("./apps/game-pipa/src")}/$1` },
];

export default defineConfig({
  test: {
    projects: [
      {
        cacheDir,
        resolve: { alias },
        test: {
          name: "unit",
          root: testsRoot,
          include: ["**/*.test.{ts,tsx}"],
          exclude: ["**/node_modules/**", "arch/**", "arch-fixtures/**", "integration/**"],
        },
      },
      {
        cacheDir,
        test: {
          name: "arch",
          root: testsRoot,
          include: ["arch/**/*.test.ts"],
          environment: "node",
        },
      },
      {
        // Integration and contract tests against the real backend started by Aspire (SPEC-0010 §7.3, §7.4):
        // `pnpm test:integration` with APOGEU_API_URL pointing at the Host. Not part of `pnpm test`.
        cacheDir,
        resolve: { alias },
        test: {
          name: "integration",
          root: testsRoot,
          include: ["integration/**/*.test.ts"],
          environment: "node",
          testTimeout: 240_000,
          hookTimeout: 240_000,
        },
      },
    ],
    coverage: {
      provider: "v8",
      include: ["apps/*/src/**/*.{ts,tsx}", "packages/*/src/**/*.{ts,tsx}"],
      reporter: ["text-summary", "lcov", "cobertura"],
      reportsDirectory: "coverage",
    },
  },
});
