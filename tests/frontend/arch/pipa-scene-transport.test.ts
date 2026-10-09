import { execFile } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, test } from "vitest";

// SPEC-0010 UT-06 (cena sem import de rede): the real scene-kit and the scene of game-pipa never reach the transport
// of game-sdk, directly or through a re-export (rule scene-does-not-import-transport of ADR-0004). Structural guard
// of the product tree; the rule itself is proven by SPEC-0003 IT-03 against fixtures.

const here = path.dirname(fileURLToPath(import.meta.url));
const frontendRoot = path.resolve(here, "../../../src/frontend");
const depcruiseBin = path.join(frontendRoot, "node_modules", ".bin", "depcruise");

interface Violation {
  readonly from: string;
  readonly to: string;
  readonly rule: { readonly name: string };
}

function cruise(): Promise<readonly Violation[]> {
  return new Promise((resolve, reject) => {
    execFile(
      depcruiseBin,
      ["--config", ".dependency-cruiser.cjs", "--output-type", "json", "apps", "packages"],
      { cwd: frontendRoot, maxBuffer: 32 * 1024 * 1024 },
      (_error, stdout, stderr) => {
        try {
          const report = JSON.parse(stdout) as {
            summary: { violations: Violation[] };
            modules: { source: string }[];
          };
          const scene = report.modules.filter((module) =>
            module.source.startsWith("packages/scene-kit/src/"),
          );
          if (scene.length === 0) {
            reject(new Error("dependency-cruiser did not see packages/scene-kit/src"));
            return;
          }
          resolve(report.summary.violations);
        } catch {
          reject(
            new Error(`dependency-cruiser did not produce a JSON report:\n${stdout}${stderr}`),
          );
        }
      },
    );
  });
}

describe("SPEC-0010 cena sem import de rede", () => {
  test("SPEC-0010:UT-06 scene-kit e a cena do game-pipa não alcançam o transporte do game-sdk", async () => {
    const violations = await cruise();

    expect(
      violations.filter((violation) => violation.rule.name === "scene-does-not-import-transport"),
    ).toEqual([]);
  }, 60_000);
});
