// Round state mirrored from the server (SPEC-0010 §6, ADR-0004): the single source the HUD (useSyncExternalStore) and
// the scene (direct subscription) read. It never computes a crash point: it only holds what the server sent.

/** Phase of the current round; `idle` until the first snapshot arrives. */
export type RoundPhase = "idle" | "betting" | "flying" | "crashed" | "annulled";

/** State of the real-time connection; `reconnecting` drives the "Reconectando…" badge. */
export type ConnectionStatus = "connecting" | "connected" | "reconnecting" | "disconnected";

/** A finished round of the history, newest first. */
export interface HistoryEntry {
  readonly roundId: string;
  readonly roundNumber: number;
  /** Decimal string with 2 places, exactly as the server sent it (ADR-0008). */
  readonly crashPoint: string;
}

/** RoundState of SPEC-0010 §6, plus the server data the HUD and the scene need to interpolate and count down. */
export interface RoundState {
  readonly phase: RoundPhase;
  readonly roundId: string | null;
  readonly roundNumber: number | null;
  /** SHA-256 commitment of the round seed (hex), published before betting. */
  readonly commitment: string | null;
  /** ISO-8601 UTC instant the betting phase ends (server clock). */
  readonly bettingEndsAt: string | null;
  /** ISO-8601 UTC instant the flight started (server clock); null before the flight. */
  readonly startedAt: string | null;
  /** Growth constant k per millisecond, as the decimal string the server sent ("0.00006"). */
  readonly growthK: string | null;
  /** Multiplier shown now: m(t) = e^(k·t) floored to 2 places while flying, the crash point once crashed, 1 before. */
  readonly multiplier: number;
  /** Final crash point (decimal string), only after RoundCrashed. */
  readonly crashPoint: string | null;
  /** Revealed seed of the round (hex), only after RoundCrashed. */
  readonly revealedSeed: string | null;
  readonly history: readonly HistoryEntry[];
  /** Clock offset: serverClock − localClock, in milliseconds (server time = local time + serverOffsetMs). */
  readonly serverOffsetMs: number;
  readonly connection: ConnectionStatus;
}

/** Read side of the store: what the HUD and the scene may use (never the transport). */
export interface RoundStateSource {
  getState(): RoundState;
  /** useSyncExternalStore-compatible subscription; returns the unsubscribe function. */
  subscribe(listener: () => void): () => void;
}
