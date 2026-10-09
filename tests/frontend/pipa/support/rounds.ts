// Fixtures of SPEC-0010 shaped like the contracts it consumes: RoundsHub messages and RoundSnapshot of SPEC-0008@2,
// session response of SPEC-0009@1. Used by the unit tests instead of the real backend.
import type { HistoryEntry, RoundEvent, SnapshotRound } from "@apogeu/game-sdk/state";
import type { Multiplier, RoundSnapshot, SessionResponse } from "@apogeu/game-sdk";

export const salaId = "demo";
export const growthK = "0.00006";
export const commitment = "4f0c5b7d1e9a2c3b6d8e0f1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d5e";
export const revealedSeed = "e2e2f30e357b0e33ff1102bd873f7b10299418647d96615c3e7475d951f7128f";

/** Identity of round `n` of the demo sala: id `round-n`. */
export function roundIds(roundNumber: number): { roundId: string; roundNumber: number } {
  return { roundId: `round-${String(roundNumber)}`, roundNumber };
}

export const iso = (epochMs: number): string => new Date(epochMs).toISOString();

export function multiplier(value: string): Multiplier {
  return value as Multiplier;
}

export function bettingEvent(roundNumber: number, bettingEndsAtMs: number): RoundEvent {
  return {
    type: "betting",
    salaId,
    ...roundIds(roundNumber),
    commitment,
    bettingEndsAt: iso(bettingEndsAtMs),
  };
}

export function flyingEvent(roundNumber: number, startedAtMs: number): RoundEvent {
  return {
    type: "flying",
    roundId: roundIds(roundNumber).roundId,
    startedAt: iso(startedAtMs),
    growthK,
  };
}

export function syncEvent(roundNumber: number, serverTimeMs: number, value: string): RoundEvent {
  return {
    type: "sync",
    roundId: roundIds(roundNumber).roundId,
    serverTime: iso(serverTimeMs),
    multiplier: value,
  };
}

export function crashedEvent(
  roundNumber: number,
  crashPoint: string,
  crashedAtMs: number,
): RoundEvent {
  return {
    type: "crashed",
    roundId: roundIds(roundNumber).roundId,
    crashPoint,
    crashedAt: iso(crashedAtMs),
    revealedSeed,
  };
}

export function snapshotEvent(
  current: SnapshotRound | null,
  history: readonly HistoryEntry[] = [],
): RoundEvent {
  return { type: "snapshot", salaId, growthK, current, history };
}

export function bettingRound(roundNumber: number, bettingEndsAtMs: number): SnapshotRound {
  return {
    ...roundIds(roundNumber),
    state: "betting",
    commitment,
    bettingEndsAt: iso(bettingEndsAtMs),
    startedAt: null,
    crashPoint: null,
    crashedAt: null,
    revealedSeed: null,
  };
}

/** RoundSnapshot of SPEC-0008@2 (JoinSala result): crashPoint and revealedSeed null until the crash. */
export function wireSnapshot(
  current: RoundSnapshot["current"],
  history: { roundNumber: number; crashPoint: string }[],
): RoundSnapshot {
  return {
    sala: { id: salaId, rtp: "0.97", cap: multiplier("10000.00"), growthK },
    current,
    history: history.map((entry) => ({
      ...roundIds(entry.roundNumber),
      crashPoint: multiplier(entry.crashPoint),
    })),
  };
}

/** POST /api/v1/sessions 200 body of SPEC-0009@1. */
export const sessionResponse: SessionResponse = {
  sessionToken: "session-token-of-the-test",
  expiresAt: "2026-10-09T13:00:00.000Z",
  player: { nickname: "Jogador Demo" },
  salaId,
  currency: "BRL",
  mode: "demo",
};
