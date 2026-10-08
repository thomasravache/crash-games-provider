// Vitest projects of the front-end (ADR-0009). Vitest 5 has no workspace file discovery, so the package.json scripts pass
// this file with --config. The tests live in tests/frontend (outside the pnpm workspace) and run from it as project root.
import { defineConfig } from "vitest/config";

const testsRoot = "../../tests/frontend";
// Keeps the Vite/Vitest cache in src/frontend/node_modules instead of creating one under tests/frontend.
const cacheDir = "../../src/frontend/node_modules/.vite";

export default defineConfig({
  test: {
    projects: [
      {
        cacheDir,
        test: {
          name: "unit",
          root: testsRoot,
          include: ["**/*.test.{ts,tsx}"],
          exclude: ["**/node_modules/**", "arch/**", "arch-fixtures/**"],
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
    ],
    coverage: {
      provider: "v8",
      include: ["apps/*/src/**/*.{ts,tsx}", "packages/*/src/**/*.{ts,tsx}"],
      reporter: ["text-summary", "lcov", "cobertura"],
      reportsDirectory: "coverage",
    },
  },
});
