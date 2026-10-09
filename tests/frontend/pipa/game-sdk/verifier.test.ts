import { verifyRound } from "@apogeu/game-sdk/fairness";
import { describe, expect, test } from "vitest";

import { fairnessVectors, tamperCrashPoint, tamperHex } from "../support/vectors";

// SPEC-0010 UT-04: the TypeScript verifier (algorithm v1 of SPEC-0007@2) accepts every shared vector and tells a
// tampered seed (chain) from a tampered crash point.

describe("SPEC-0010 verificador provably fair no navegador", () => {
  test("SPEC-0010:UT-04 os vetores compartilhados existem (pré-condição)", () => {
    expect(fairnessVectors.length).toBeGreaterThanOrEqual(6);
  });

  test.each(fairnessVectors.map((vector) => [vector.description ?? vector.seed, vector] as const))(
    "SPEC-0010:UT-04 vetor válido → valid (%s)",
    async (_name, vector) => {
      await expect(verifyRound(vector)).resolves.toBe("valid");
    },
  );

  test.each(fairnessVectors.map((vector) => [vector.description ?? vector.seed, vector] as const))(
    "SPEC-0010:UT-04 semente alterada → chain-mismatch (%s)",
    async (_name, vector) => {
      await expect(verifyRound({ ...vector, seed: tamperHex(vector.seed) })).resolves.toBe(
        "chain-mismatch",
      );
      await expect(
        verifyRound({ ...vector, previousSeed: tamperHex(vector.previousSeed) }),
      ).resolves.toBe("chain-mismatch");
    },
  );

  test.each(fairnessVectors.map((vector) => [vector.description ?? vector.seed, vector] as const))(
    "SPEC-0010:UT-04 crash alterado → crash-mismatch (%s)",
    async (_name, vector) => {
      await expect(
        verifyRound({ ...vector, crashPoint: tamperCrashPoint(vector.crashPoint) }),
      ).resolves.toBe("crash-mismatch");
    },
  );
});
