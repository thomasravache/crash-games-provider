// "Todas as apostas" (SPEC-0031, SPEC-0025@4): the bets of the sala in the current round, as the server sent them
// (JoinSalaBets snapshot, then the LiveBetsBatch messages). Nicknames arrive anonymized ("Ma***"); money and
// multipliers stay decimal strings (ADR-0008). A RoundBetting of another round, from the round store, empties the list.
//
// SCAFFOLD (SPEC-0031): signatures only; the reducer and the store are implemented in the green phase.

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

/** Next live bets state for one event. */
export function reduceLiveBetsEvent(_state: LiveBetsState, event: LiveBetsEvent): LiveBetsState {
  throw new Error(`SPEC-0031: reduceLiveBetsEvent(${event.type}) is not implemented yet`);
}

export function createLiveBetsStore(): LiveBetsStore {
  return {
    getState: () => initialLiveBetsState,
    subscribe: () => () => undefined,
    dispatch(event) {
      reduceLiveBetsEvent(initialLiveBetsState, event);
    },
  };
}
