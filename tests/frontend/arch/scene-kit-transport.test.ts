import { execFile } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, test } from "vitest";

// SPEC-0003 IT-03: the dependency-cruiser rules of ADR-0004 reject scene-kit importing the transport of game-sdk.
// The fixture tree under tests/frontend/arch-fixtures mirrors src/frontend (packages/scene-kit, packages/game-sdk), so
// dependency-cruiser runs from the fixture root with the real configuration and sees the same paths as in the product.

const here = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(here, "../../..");
const frontendRoot = path.join(repositoryRoot, "src", "frontend");
const configPath = path.join(frontendRoot, ".dependency-cruiser.cjs");
const depcruiseBin = path.join(frontendRoot, "node_modules", ".bin", "depcruise");
const fixtureRoot = path.resolve(here, "..", "arch-fixtures");
// Separate tree: its game-sdk entry re-exports the transport, so it cannot share the allowed control above.
const entryReexportRoot = path.join(fixtureRoot, "entry-reexport");
const transportRule = "scene-does-not-import-transport";

const violatingModule = "packages/scene-kit/src/transport-violation.ts";
const transportModule = "packages/game-sdk/src/transport/socket-client.ts";
const allowedModule = "packages/scene-kit/src/state-reader.ts";
const throughEntryModule = "packages/scene-kit/src/transport-through-entry.ts";

interface Violation {
  readonly from: string;
  readonly to: string;
  readonly rule: { readonly name: string; readonly severity: string };
}

interface CruiseRun {
  readonly violations: readonly Violation[];
}

function cruiseFixture(root: string): Promise<CruiseRun> {
  return new Promise((resolve, reject) => {
    execFile(
      depcruiseBin,
      ["--config", configPath, "--output-type", "json", "packages"],
      { cwd: root, maxBuffer: 16 * 1024 * 1024 },
      (error, stdout, stderr) => {
        const exitCode = error === null ? 0 : typeof error.code === "number" ? error.code : -1;
        try {
          const result = JSON.parse(stdout) as { summary: { violations: Violation[] } };
          resolve({ violations: result.summary.violations });
        } catch {
          reject(
            new Error(
              `dependency-cruiser did not produce a JSON report (exit ${String(exitCode)}):\n${stdout}${stderr}`,
            ),
          );
        }
      },
    );
  });
}

describe("dependency-cruiser rules of ADR-0004", () => {
  test("SPEC-0003:IT-03 dependency-cruiser reprova o scene-kit importando o transporte do game-sdk", async () => {
    expect(existsSync(depcruiseBin), `dependency-cruiser is not installed (${depcruiseBin})`).toBe(
      true,
    );
    expect(
      existsSync(configPath),
      `missing dependency-cruiser configuration (ADR-0004): ${configPath}`,
    ).toBe(true);

    const run = await cruiseFixture(fixtureRoot);

    // The JSON reporter always exits 0: the rejection is an error-severity violation in the report.
    expect(run.violations).toContainEqual(
      expect.objectContaining({
        from: violatingModule,
        to: transportModule,
        rule: expect.objectContaining({ name: transportRule, severity: "error" }) as unknown,
      }),
    );
    // Control: reading the state exposed by game-sdk is allowed.
    expect(run.violations.filter((violation) => violation.from === allowedModule)).toEqual([]);
  }, 60_000);

  test("SPEC-0003:IT-03 dependency-cruiser reprova o scene-kit obtendo o transporte pela entrada do pacote game-sdk", async () => {
    expect(
      existsSync(configPath),
      `missing dependency-cruiser configuration (ADR-0004): ${configPath}`,
    ).toBe(true);

    const run = await cruiseFixture(entryReexportRoot);

    expect(run.violations).toContainEqual(
      expect.objectContaining({
        from: throughEntryModule,
        rule: expect.objectContaining({ name: transportRule, severity: "error" }) as unknown,
      }),
    );
  }, 60_000);
});
