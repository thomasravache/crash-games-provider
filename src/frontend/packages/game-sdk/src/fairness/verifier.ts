// Provably fair verifier in the browser (SPEC-0010 §4, ADR-0006): algorithm v1 of SPEC-0007@2
// (tests/vectors/README.md) in exact integer arithmetic (BigInt), tested with the same vectors as the C# module.

import { NotImplementedError } from "../not-implemented";

/** Data of one finished round (GET /api/v1/rounds/{roundId}/verification, vectors of SPEC-0007@2). */
export interface RoundVerificationData {
  /** Revealed seed s_r, lowercase hex of 32 bytes. */
  readonly seed: string;
  /** s_{r-1} = SHA256(s_r), lowercase hex. */
  readonly previousSeed: string;
  /** Chain salt, lowercase hex of its raw bytes. */
  readonly salt: string;
  /** RTP as a decimal string ("0.97"). */
  readonly rtp: string;
  /** Cap as a decimal string with 2 places ("10000.00"). */
  readonly cap: string;
  /** Crash point the server claims, decimal string with 2 places. */
  readonly crashPoint: string;
}

export type VerificationResult = "valid" | "chain-mismatch" | "crash-mismatch";

/** Crash point of algorithm v1 as a decimal string with 2 places. */
export function computeCrashPoint(
  input: Pick<RoundVerificationData, "seed" | "salt" | "rtp" | "cap">,
): Promise<string> {
  return Promise.reject(new NotImplementedError("computeCrashPoint", input));
}

/** valid | chain-mismatch (SHA256(seed) ≠ previousSeed) | crash-mismatch (recomputed crash ≠ claimed crash). */
export function verifyRound(data: RoundVerificationData): Promise<VerificationResult> {
  return Promise.reject(new NotImplementedError("verifyRound", data));
}
