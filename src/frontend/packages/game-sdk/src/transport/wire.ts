// Wire types of the RoundsHub (SPEC-0008@2 §6) and of the session exchange (SPEC-0009@1 §6), taken from the generated
// contracts (ADR-0008): never written by hand here, so a contract change breaks the build of the client.

import type { components, RoundsHubServer } from "@apogeu/contracts";

export type { Multiplier, RoundsHubClient, RoundsHubServer } from "@apogeu/contracts";

/** Result of JoinSala: the sala, the current round and the last 20 crashes. */
export type RoundSnapshot = Awaited<ReturnType<RoundsHubServer["JoinSala"]>>;

/** POST /api/v1/sessions request body (SPEC-0009@1). */
export type SessionRequest = components["schemas"]["CreateSessionRequest"];

/** POST /api/v1/sessions 200 body (SPEC-0009@1). */
export type SessionResponse = components["schemas"]["SessionResponse"];

/** Error codes of POST /api/v1/sessions (ProblemDetails `code`). */
export type SessionErrorCode =
  "LAUNCH_TOKEN_INVALID" | "LAUNCH_TOKEN_EXPIRED" | "LAUNCH_TOKEN_USED" | "RATE_LIMITED";
