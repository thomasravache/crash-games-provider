// Round events the store reduces (SPEC-0010 §6). They carry the data of the RoundsHub messages (SPEC-0008@2) but are
// state types, not transport types: the client maps each hub message to one event, so the scene never sees the wire.

import type { HistoryEntry } from "./round-state";

/** The current round as the snapshot describes it (RoundSnapshot.current of SPEC-0008@2). */
export interface SnapshotRound {
  readonly roundId: string;
  readonly roundNumber: number;
  readonly state: "betting" | "flying" | "crashed" | "annulled";
  readonly commitment: string;
  readonly bettingEndsAt: string;
  readonly startedAt: string | null;
  readonly crashPoint: string | null;
  readonly crashedAt: string | null;
  readonly revealedSeed: string | null;
}

export type RoundEvent =
  | {
      readonly type: "snapshot";
      readonly salaId: string;
      readonly growthK: string;
      readonly current: SnapshotRound | null;
      readonly history: readonly HistoryEntry[];
    }
  | {
      readonly type: "betting";
      readonly salaId: string;
      readonly roundId: string;
      readonly roundNumber: number;
      readonly commitment: string;
      readonly bettingEndsAt: string;
    }
  | {
      readonly type: "flying";
      readonly roundId: string;
      readonly startedAt: string;
      readonly growthK: string;
    }
  | {
      readonly type: "sync";
      readonly roundId: string;
      readonly serverTime: string;
      readonly multiplier: string;
    }
  | {
      readonly type: "crashed";
      readonly roundId: string;
      readonly crashPoint: string;
      readonly crashedAt: string;
      readonly revealedSeed: string;
    }
  | { readonly type: "annulled"; readonly roundId: string; readonly reason: string };
