// Generated from the backend SignalR hubs (ADR-0008, SPEC-0006). Never edit by hand.

import type { Money, Multiplier } from "../scalars";

/** Names of the SignalR hubs the backend serves. */
export const hubNames = ["BetsHub", "RoundsHub"] as const;

export type HubName = (typeof hubNames)[number];

/** Methods the client invokes on BetsHub. */
export interface BetsHubServer {
  CancelBet(request: { betId: string }): Promise<{
    betId: string;
    refunded: Money;
    refundStatus: "refunded" | "pending";
    balance: Money | null;
  }>;
  CashOut(request: { betId: string }): Promise<{
    cashedOut: {
      betId: string;
      multiplier: Multiplier;
      payout: Money;
      creditStatus: "credited" | "pending";
      balance: Money | null;
      auto: boolean;
    } | null;
    pending: { betId: string } | null;
  }>;
  GetBetLimits(): Promise<{ currency: string; minBet: Money; maxBet: Money; maxWin: Money }>;
  PlaceBet(request: {
    clientBetId: string;
    panel: number;
    amount: Money;
    autoCashOut: Multiplier | null;
  }): Promise<{
    betId: string;
    clientBetId: string;
    roundId: string | null;
    roundNumber: number | null;
    panel: number;
    amount: Money;
    currency: string;
    autoCashOut: Multiplier | null;
    status: "placed" | "queued";
    balance: Money;
  }>;
}

/** Methods BetsHub calls on the client. */
export interface BetsHubClient {
  BalanceUpdated(message: { balance: Money; currency: string }): void;
  BetActivated(message: { betId: string; roundId: string; roundNumber: number }): void;
  BetRefunded(message: {
    betId: string;
    roundId: string;
    refunded: Money;
    balance: Money | null;
    refundStatus: "refunded" | "pending";
  }): void;
  BetSettled(message: { betId: string; roundId: string; status: "lost" }): void;
  CashOutRejected(message: { betId: string; code: string }): void;
  CashedOut(message: {
    betId: string;
    multiplier: Multiplier;
    payout: Money;
    creditStatus: "credited" | "pending";
    balance: Money | null;
    auto: boolean;
  }): void;
}

/** Methods the client invokes on RoundsHub. */
export interface RoundsHubServer {
  GetServerTime(): Promise<{ serverTime: string }>;
  JoinSala(salaId: string): Promise<{
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
  }>;
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
