// Game client (SPEC-0010 §6): exchanges the launch token, connects to the hub with reconnection, estimates the clock
// offset (GetServerTime), joins the sala and maps every hub message to a store event. Resynchronizes from a fresh
// RoundSnapshot after each reconnection.

import { NotImplementedError } from "./not-implemented";
import type { RoundStore } from "./state/store";
import type { ConnectHub } from "./transport/hub-connection";
import type { FetchLike } from "./transport/session-api";

export interface GameClientOptions {
  /** Base URL of the API (Host), without a trailing slash; the hub is `${apiBaseUrl}/hubs/rounds`. */
  readonly apiBaseUrl: string;
  /** HTTP client; defaults to the global fetch. */
  readonly fetch?: FetchLike;
  /** Hub connection factory; defaults to createSignalRConnection. */
  readonly connectHub?: ConnectHub;
  /** Local clock (epoch ms); defaults to Date.now. */
  readonly now?: () => number;
}

/** What the session exchange returned that the game may show (nothing personal besides the nickname). */
export interface SessionInfo {
  readonly nickname: string;
  readonly salaId: string;
  readonly currency: string;
  readonly mode: string;
  readonly expiresAt: string;
}

export interface GameClient {
  readonly store: RoundStore;
  /** Exchanges the launch token (rejects with SessionError), connects, syncs the clock and joins the sala. */
  start(launchToken: string): Promise<SessionInfo>;
  stop(): Promise<void>;
}

export function createGameClient(options: GameClientOptions): GameClient {
  throw new NotImplementedError("createGameClient", options);
}
