// Verification data of a finished round (SPEC-0008@2 §6): GET /api/v1/rounds/{roundId}/verification.

import type { RoundVerificationData } from "../fairness/verifier";
import { problemCode, type FetchLike } from "./session-api";

/** The verification data could not be read: ROUND_NOT_FOUND, ROUND_NOT_FINISHED, or a network/unknown failure. */
export class VerificationError extends Error {
  readonly code: string;

  constructor(code: string) {
    super(code);
    this.name = "VerificationError";
    this.code = code;
  }
}

export async function fetchRoundVerification(
  apiBaseUrl: string,
  roundId: string,
  fetchImpl: FetchLike,
): Promise<RoundVerificationData> {
  let response: Response;
  try {
    response = await fetchImpl(
      `${apiBaseUrl}/api/v1/rounds/${encodeURIComponent(roundId)}/verification`,
      { headers: { accept: "application/json" } },
    );
  } catch {
    throw new VerificationError("NETWORK");
  }
  if (!response.ok) throw new VerificationError((await problemCode(response)) ?? "UNKNOWN");
  const body = (await response.json()) as Partial<Record<keyof RoundVerificationData, unknown>>;
  const fields = ["seed", "previousSeed", "salt", "rtp", "cap", "crashPoint"] as const;
  if (!fields.every((field) => typeof body[field] === "string")) {
    throw new VerificationError("UNKNOWN");
  }
  return body as RoundVerificationData;
}
