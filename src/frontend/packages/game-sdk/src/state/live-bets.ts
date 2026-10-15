// "Todas as apostas" (SPEC-0031, SPEC-0025@4): the bets of the sala in the current round, as the server sent them
// (JoinSalaBets snapshot, then the LiveBetsBatch messages). Nicknames arrive anonymized ("Ma***"); money and
// multipliers stay decimal strings (ADR-0008). A RoundBetting of another round, from the round store, empties the list.

import type { Money, Multiplier } from "@apogeu/contracts";

/** A bet of the sala in the round (LiveBet of SPEC-0025@4). */
export interface LiveBet {
  /** Opaque reference of the bet (never the betId). */
  readonly betRef: string;
  /** Anonymized nickname: "Ma***", "Jogador***". */
  readonly nickname: string;
  readonly amount: Money;
  readonly currency: string;
  /** Cash-out multiplier, or null while the bet is open. */
  readonly multiplier: Multiplier | null;
  /** Cash-out payout, or null while the bet is open. */
  readonly payout: Money | null;
}

/** A cash-out of the round (LiveBetsBatch.cashedOut[] of SPEC-0025@4). */
export interface LiveCashOut {
  readonly betRef: string;
  readonly multiplier: Multiplier;
  readonly payout: Money;
}

export interface LiveBetsState {
  /** Round the list belongs to; null before the first snapshot. */
  readonly roundId: string | null;
  readonly bets: readonly LiveBet[];
  readonly totalBets: number;
  /** Distinct players with a bet in the round. */
  readonly totalPlayers: number;
  /** Total wagered in the round; null before the first snapshot. */
  readonly totalWagered: Money | null;
}

/** Events of the live bets store. */
export type LiveBetsEvent =
  | {
      /** JoinSalaBets result (LiveBetsSnapshot). */
      readonly type: "liveSnapshot";
      readonly roundId: string;
      readonly bets: readonly LiveBet[];
      readonly totalBets: number;
      readonly totalPlayers: number;
      readonly totalWagered: Money;
    }
  | {
      /** LiveBetsBatch message. */
      readonly type: "liveBatch";
      readonly roundId: string;
      readonly placed: readonly LiveBet[];
      readonly cashedOut: readonly LiveCashOut[];
      readonly totalBets: number;
      readonly totalPlayers: number;
      readonly totalWagered: Money;
    }
  | {
      /** RoundBetting of a round, from the round store: another round empties the list. */
      readonly type: "liveRoundBetting";
      readonly roundId: string;
    };

export interface LiveBetsStateSource {
  getState(): LiveBetsState;
  subscribe(listener: () => void): () => void;
}

export interface LiveBetsStore extends LiveBetsStateSource {
  dispatch(event: LiveBetsEvent): void;
}

export const initialLiveBetsState: LiveBetsState = {
  roundId: null,
  bets: [],
  totalBets: 0,
  totalPlayers: 0,
  totalWagered: null,
};

/** Upserts the placed bets (by betRef) and applies the cash-outs to the list. */
function applyBets(
  bets: readonly LiveBet[],
  placed: readonly LiveBet[],
  cashedOut: readonly LiveCashOut[],
): LiveBet[] {
  const next = [...bets];
  for (const bet of placed) {
    const index = next.findIndex((existing) => existing.betRef === bet.betRef);
    if (index < 0) next.push(bet);
    else next[index] = bet;
  }
  for (const cashOut of cashedOut) {
    const index = next.findIndex((existing) => existing.betRef === cashOut.betRef);
    const bet = next[index];
    if (bet === undefined) continue;
    next[index] = { ...bet, multiplier: cashOut.multiplier, payout: cashOut.payout };
  }
  return next;
}

/** Next live bets state for one event. */
export function reduceLiveBetsEvent(state: LiveBetsState, event: LiveBetsEvent): LiveBetsState {
  switch (event.type) {
    case "liveSnapshot":
      return {
        roundId: event.roundId,
        bets: applyBets([], event.bets, []),
        totalBets: event.totalBets,
        totalPlayers: event.totalPlayers,
        totalWagered: event.totalWagered,
      };
    case "liveBatch":
      // A batch of another round (it may arrive before the RoundBetting) starts the list of that round over.
      return {
        roundId: event.roundId,
        bets: applyBets(
          event.roundId === state.roundId ? state.bets : [],
          event.placed,
          event.cashedOut,
        ),
        totalBets: event.totalBets,
        totalPlayers: event.totalPlayers,
        totalWagered: event.totalWagered,
      };
    case "liveRoundBetting":
      if (event.roundId === state.roundId) return state;
      return { ...initialLiveBetsState, roundId: event.roundId };
  }
}

export function createLiveBetsStore(initial: LiveBetsState = initialLiveBetsState): LiveBetsStore {
  let state = initial;
  const listeners = new Set<() => void>();
  return {
    getState: () => state,
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    dispatch(event) {
      const next = reduceLiveBetsEvent(state, event);
      if (next === state) return;
      state = next;
      for (const listener of [...listeners]) listener();
    },
  };
}
