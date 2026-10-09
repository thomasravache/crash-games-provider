// Wire types of the RoundsHub (SPEC-0008@2 §6) and of the session exchange (SPEC-0009@1 §6).
//
// TEMPORARY MIRROR: the generated contracts of SPEC-0008/SPEC-0009 (ADR-0008) are not in this branch yet. Same names
// and shapes as `RoundsHubServer` / `RoundsHubClient` of @apogeu/contracts; at integration this file becomes a
// re-export of the generated types (`export type { … } from "@apogeu/contracts"`) and the hand-written shapes go away.

/** Decimal string (for example "2.47"), same brand as @apogeu/contracts. */
export type Multiplier = string & { readonly __brand: "Multiplier" };

export interface RoundSnapshot {
  sala: { id: string; rtp: string; cap: Multiplier; growthK: string };
  current: {
    roundId: string;
    roundNumber: number;
    state: "betting" | "flying" | "crashed" | "annulled";
    commitment: string;
    bettingEndsAt: string;
    startedAt: string | null;
    crashPoint: Multiplier | null;
    crashedAt: string | null;
    revealedSeed: string | null;
  } | null;
  history: { roundId: string; roundNumber: number; crashPoint: Multiplier }[];
}

/** Methods the client invokes on RoundsHub. */
export interface RoundsHubServer {
  GetServerTime(): Promise<{ serverTime: string }>;
  JoinSala(salaId: string): Promise<RoundSnapshot>;
}

/** Methods RoundsHub calls on the client. */
export interface RoundsHubClient {
  RoundAnnulled(message: { roundId: string; reason: string }): void;
  RoundBetting(message: {
    salaId: string;
    roundId: string;
    roundNumber: number;
    commitment: string;
    bettingEndsAt: string;
  }): void;
  RoundCrashed(message: {
    roundId: string;
    crashPoint: Multiplier;
    crashedAt: string;
    revealedSeed: string;
  }): void;
  RoundFlying(message: { roundId: string; startedAt: string; growthK: string }): void;
  RoundSync(message: { roundId: string; serverTime: string; multiplier: Multiplier }): void;
}

/** POST /api/v1/sessions request body (SPEC-0009@1). */
export interface SessionRequest {
  launchToken: string;
}

/** POST /api/v1/sessions 200 body (SPEC-0009@1). */
export interface SessionResponse {
  sessionToken: string;
  expiresAt: string;
  player: { nickname: string };
  salaId: string;
  currency: string;
  mode: string;
}

/** Error codes of POST /api/v1/sessions (ProblemDetails `code`). */
export type SessionErrorCode =
  "LAUNCH_TOKEN_INVALID" | "LAUNCH_TOKEN_EXPIRED" | "LAUNCH_TOKEN_USED" | "RATE_LIMITED";
