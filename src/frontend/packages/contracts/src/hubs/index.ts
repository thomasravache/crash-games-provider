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
  BetActivated(message: { betId: string; roundId: string; roundNumber: number }): void;
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
