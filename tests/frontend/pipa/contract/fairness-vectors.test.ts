import { computeCrashPoint } from "@apogeu/game-sdk/fairness";
import { describe, expect, test } from "vitest";

import { fairnessVectors } from "../support/vectors";

// SPEC-0010 CT-01: contract SPEC-0007@2 — the vectors are the results of the C# Fairness module (its UT-01/CT-01 read
// the same file); the TypeScript verifier recomputes every crash point and gets exactly the same string.

describe("SPEC-0010 contrato dos vetores provably fair (SPEC-0007@2)", () => {
  test("SPEC-0010:CT-01 o verificador TS recalcula todos os vetores com resultado idêntico ao do C#", async () => {
    expect(fairnessVectors.length).toBeGreaterThanOrEqual(6);

    const recomputed = await Promise.all(
      fairnessVectors.map(async (vector) => ({
        description: vector.description,
        crashPoint: await computeCrashPoint(vector),
      })),
    );

    expect(recomputed).toEqual(
      fairnessVectors.map((vector) => ({
        description: vector.description,
        crashPoint: vector.crashPoint,
      })),
    );
  });
});
