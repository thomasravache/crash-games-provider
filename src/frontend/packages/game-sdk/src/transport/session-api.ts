// Launch token → session exchange (SPEC-0009@1): POST /api/v1/sessions.

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

  /** The launch token cannot open a session (invalid, expired or already used): back to the casino. */
  get isLaunchTokenRejected(): boolean {
    return (
      this.code === "LAUNCH_TOKEN_INVALID" ||
      this.code === "LAUNCH_TOKEN_EXPIRED" ||
      this.code === "LAUNCH_TOKEN_USED"
    );
  }
}

const sessionErrorCodes: readonly SessionErrorCode[] = [
  "LAUNCH_TOKEN_INVALID",
  "LAUNCH_TOKEN_EXPIRED",
  "LAUNCH_TOKEN_USED",
  "RATE_LIMITED",
];

function isSessionErrorCode(value: unknown): value is SessionErrorCode {
  return sessionErrorCodes.some((code) => code === value);
}

/** `code` of a ProblemDetails body (RFC 9457 + extension `code`), when present. */
export async function problemCode(response: Response): Promise<string | undefined> {
  try {
    const body = (await response.json()) as unknown;
    if (typeof body === "object" && body !== null && "code" in body) {
      const { code } = body;
      return typeof code === "string" ? code : undefined;
    }
  } catch {
    // Not JSON: no code.
  }
  return undefined;
}

function isSessionResponse(body: unknown): body is SessionResponse {
  if (typeof body !== "object" || body === null) return false;
  const candidate = body as Partial<Record<keyof SessionResponse, unknown>>;
  return (
    typeof candidate.sessionToken === "string" &&
    typeof candidate.salaId === "string" &&
    typeof candidate.player === "object" &&
    candidate.player !== null
  );
}

export async function exchangeLaunchToken(
  apiBaseUrl: string,
  launchToken: string,
  fetchImpl: FetchLike,
): Promise<SessionResponse> {
  let response: Response;
  try {
    response = await fetchImpl(`${apiBaseUrl}/api/v1/sessions`, {
      method: "POST",
      headers: { "content-type": "application/json", accept: "application/json" },
      body: JSON.stringify({ launchToken }),
    });
  } catch {
    throw new SessionError("NETWORK", "The session exchange could not reach the server.");
  }

  if (!response.ok) {
    const code = await problemCode(response);
    throw isSessionErrorCode(code)
      ? new SessionError(code)
      : new SessionError(
          "UNKNOWN",
          `Session exchange failed with HTTP ${String(response.status)}.`,
        );
  }

  const body = (await response.json()) as unknown;
  if (!isSessionResponse(body)) {
    throw new SessionError("UNKNOWN", "The session response does not match the contract.");
  }
  return body;
}
