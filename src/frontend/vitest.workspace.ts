// Vitest projects of the front-end (ADR-0009). Vitest 5 has no workspace file discovery, so the package.json scripts pass
// this file with --config. The tests live in tests/frontend (outside the pnpm workspace) and run from it as project root.
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

const testsRoot = fileURLToPath(new URL("../../tests/frontend", import.meta.url));
// Keeps the Vite/Vitest cache inside src/frontend/node_modules instead of creating one under tests/frontend.
const cacheDir = fileURLToPath(new URL("node_modules/.vite", import.meta.url));

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
