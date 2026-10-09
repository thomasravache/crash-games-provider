// Launch token → session exchange (SPEC-0009@1): POST /api/v1/sessions.

import { NotImplementedError } from "../not-implemented";
import type { SessionErrorCode, SessionResponse } from "./wire";

export type FetchLike = (input: string, init?: RequestInit) => Promise<Response>;

/** The exchange failed: invalid, expired or used launch token, rate limit, or a network/unknown failure. */
export class SessionError extends Error {
  readonly code: SessionErrorCode | "NETWORK" | "UNKNOWN";

  constructor(code: SessionErrorCode | "NETWORK" | "UNKNOWN", message?: string) {
    super(message ?? code);
    this.name = "SessionError";
    this.code = code;
  }
}

export function exchangeLaunchToken(
  apiBaseUrl: string,
  launchToken: string,
  fetchImpl: FetchLike,
): Promise<SessionResponse> {
  return Promise.reject(
    new NotImplementedError("exchangeLaunchToken", apiBaseUrl, fetchImpl, launchToken),
  );
}
