// Store without a framework (SPEC-0010 §4): subscribable, consumed by React through useSyncExternalStore and by the
// scene through a direct subscription. It only mirrors what the server sent (ADR-0004): it never computes a crash
// point, and stale, duplicated or out-of-order messages never move a round back.

import type { RoundEvent, SnapshotRound } from "./events";
import { multiplierAt } from "./multiplier";
import type { ConnectionStatus, HistoryEntry, RoundState, RoundStateSource } from "./round-state";

export interface RoundStore extends RoundStateSource {
  /** Reduces one server event; stale, duplicated or out-of-order events leave the state unchanged. */
  dispatch(event: RoundEvent): void;
  setConnection(status: ConnectionStatus): void;
  setServerOffset(serverOffsetMs: number): void;
  /** Recomputes the interpolated multiplier at the local instant (called every frame while flying). */
  tick(localNowMs: number): void;
}

export const initialRoundState: RoundState = {
  phase: "idle",
  roundId: null,
  roundNumber: null,
  commitment: null,
  bettingEndsAt: null,
  startedAt: null,
  growthK: null,
  multiplier: 1,
  crashPoint: null,
  revealedSeed: null,
  history: [],
  serverOffsetMs: 0,
  connection: "connecting",
};

/** Rounds kept in the history (the snapshot of SPEC-0008@2 carries the last 20). */
export const historyLimit = 20;

/** Finished round of the history, newest first, without duplicates. */
function withHistoryEntry(
  history: readonly HistoryEntry[],
  entry: HistoryEntry,
): readonly HistoryEntry[] {
  return [entry, ...history.filter((item) => item.roundId !== entry.roundId)].slice(
    0,
    historyLimit,
  );
}

function fromSnapshot(
  state: RoundState,
  growthK: string,
  current: SnapshotRound | null,
  history: readonly HistoryEntry[],
): RoundState {
  const base: RoundState = {
    ...initialRoundState,
    growthK,
    history: history.slice(0, historyLimit).map((entry) => ({ ...entry })),
    serverOffsetMs: state.serverOffsetMs,
    connection: state.connection,
  };
  if (current === null) return base;
  return {
    ...base,
    phase: current.state,
    roundId: current.roundId,
    roundNumber: current.roundNumber,
    commitment: current.commitment,
    bettingEndsAt: current.bettingEndsAt,
    startedAt: current.startedAt,
    crashPoint: current.state === "crashed" ? current.crashPoint : null,
    revealedSeed: current.state === "crashed" ? current.revealedSeed : null,
    multiplier:
      current.state === "crashed" && current.crashPoint !== null ? Number(current.crashPoint) : 1,
  };
}

/** Next state for one event; returns the same object when the event must be ignored. */
export function reduceRoundEvent(state: RoundState, event: RoundEvent): RoundState {
  switch (event.type) {
    case "snapshot":
      return fromSnapshot(state, event.growthK, event.current, event.history);

    case "betting": {
      if (event.roundId === state.roundId) return state;
      if (state.roundNumber !== null && event.roundNumber <= state.roundNumber) return state;
      return {
        ...state,
        phase: "betting",
        roundId: event.roundId,
        roundNumber: event.roundNumber,
        commitment: event.commitment,
        bettingEndsAt: event.bettingEndsAt,
        startedAt: null,
        multiplier: 1,
        crashPoint: null,
        revealedSeed: null,
      };
    }

    case "flying": {
      const sameRound = event.roundId === state.roundId;
      // The flight of the round in betting; or of a round whose betting message was missed (joined in between).
      const accepted = sameRound
        ? state.phase === "betting"
        : state.phase === "idle" || state.phase === "crashed" || state.phase === "annulled";
      if (!accepted) return state;
      return {
        ...state,
        phase: "flying",
        roundId: event.roundId,
        roundNumber: sameRound ? state.roundNumber : null,
        commitment: sameRound ? state.commitment : null,
        bettingEndsAt: sameRound ? state.bettingEndsAt : null,
        startedAt: event.startedAt,
        growthK: event.growthK,
        multiplier: 1,
        crashPoint: null,
        revealedSeed: null,
      };
    }

    case "sync": {
      if (event.roundId !== state.roundId || state.phase !== "flying") return state;
      // The server multiplier is a floor for the display: the interpolation never shows less than it.
      const value = Number(event.multiplier);
      if (!Number.isFinite(value) || value <= state.multiplier) return state;
      return { ...state, multiplier: value };
    }

    case "crashed": {
      if (event.roundId !== state.roundId) return state;
      if (state.phase !== "betting" && state.phase !== "flying") return state;
      return {
        ...state,
        phase: "crashed",
        crashPoint: event.crashPoint,
        revealedSeed: event.revealedSeed,
        multiplier: Number(event.crashPoint),
        history:
          state.roundNumber === null
            ? state.history
            : withHistoryEntry(state.history, {
                roundId: event.roundId,
                roundNumber: state.roundNumber,
                crashPoint: event.crashPoint,
              }),
      };
    }

    case "annulled": {
      if (event.roundId !== state.roundId) return state;
      if (state.phase !== "betting" && state.phase !== "flying") return state;
      return { ...state, phase: "annulled", multiplier: 1 };
    }
  }
}

export function createRoundStore(): RoundStore {
  let state = initialRoundState;
  const listeners = new Set<() => void>();

  const set = (next: RoundState): void => {
    if (next === state) return;
    state = next;
    for (const listener of [...listeners]) listener();
  };

  return {
    getState: () => state,
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    dispatch(event) {
      set(reduceRoundEvent(state, event));
    },
    setConnection(status) {
      if (status !== state.connection) set({ ...state, connection: status });
    },
    setServerOffset(serverOffsetMs) {
      if (serverOffsetMs !== state.serverOffsetMs) set({ ...state, serverOffsetMs });
    },
    tick(localNowMs) {
      if (state.phase !== "flying" || state.startedAt === null || state.growthK === null) return;
      const value = multiplierAt(
        {
          startedAtMs: Date.parse(state.startedAt),
          growthK: Number(state.growthK),
          serverOffsetMs: state.serverOffsetMs,
        },
        localNowMs,
      );
      if (value > state.multiplier) set({ ...state, multiplier: value });
    },
  };
}
