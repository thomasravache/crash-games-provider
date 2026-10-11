// Fixtures of SPEC-0026 shaped like the contracts it consumes: bet limits (SPEC-0020@2), BetsHub messages
// (SPEC-0022@3, SPEC-0023@2, SPEC-0024@2) and wallet bodies (SPEC-0021@4), as bets store events. Used by the unit tests
// instead of the real backend.
import type {
  BetDraft,
  BetLimits,
  BetsEvent,
  Money,
  Multiplier,
  PanelNumber,
} from "@apogeu/game-sdk/state";

export const apiBaseUrl = "http://api.test";
export const sessionToken = "session-token-of-the-test";

export const money = (value: string): Money => value as Money;
export const multiplier = (value: string): Multiplier => value as Multiplier;

/** Demo limits of the operator: R$ 1,00 to R$ 1.000,00, max win R$ 10.000,00. */
export const limits: BetLimits = {
  currency: "BRL",
  minBet: money("1.00"),
  maxBet: money("1000.00"),
  maxWin: money("10000.00"),
};

/** Draft of a panel: amount as typed, automatic cash-out off unless given. */
export function draft(amount: string, autoCashOutTarget?: string): BetDraft {
  return autoCashOutTarget === undefined
    ? { amount, autoCashOutEnabled: false, autoCashOutTarget: "2,00" }
    : { amount, autoCashOutEnabled: true, autoCashOutTarget };
}

type EventOf<T extends BetsEvent["type"]> = Extract<BetsEvent, { type: T }>;

export const balanceRead = (balance: string): EventOf<"balance"> => ({
  type: "balance",
  balance: money(balance),
  currency: "BRL",
});

export const limitsRead: EventOf<"limits"> = { type: "limits", limits };

export const placeRequested = (
  panel: PanelNumber,
  clientBetId: string,
  amount: string,
  autoCashOut: string | null = null,
): EventOf<"placeRequested"> => ({
  type: "placeRequested",
  panel,
  clientBetId,
  amount: money(amount),
  autoCashOut: autoCashOut === null ? null : multiplier(autoCashOut),
});

export function betAccepted(options: {
  readonly panel: PanelNumber;
  readonly betId: string;
  readonly clientBetId: string;
  readonly roundId: string | null;
  readonly amount: string;
  readonly status: "placed" | "queued";
  readonly balance: string;
  readonly autoCashOut?: string;
}): EventOf<"betAccepted"> {
  return {
    type: "betAccepted",
    betId: options.betId,
    clientBetId: options.clientBetId,
    roundId: options.roundId,
    roundNumber:
      options.roundId === null ? null : roundNumberOf(options.roundId),
    panel: options.panel,
    amount: money(options.amount),
    currency: "BRL",
    autoCashOut:
      options.autoCashOut === undefined
        ? null
        : multiplier(options.autoCashOut),
    status: options.status,
    balance: money(options.balance),
  };
}

export const betActivated = (
  betId: string,
  roundId: string,
): EventOf<"betActivated"> => ({
  type: "betActivated",
  betId,
  roundId,
  roundNumber: roundNumberOf(roundId),
});

export function cashedOut(options: {
  readonly betId: string;
  readonly multiplier: string;
  readonly payout: string;
  readonly creditStatus?: "credited" | "pending";
  readonly balance: string | null;
  readonly auto?: boolean;
}): EventOf<"cashedOut"> {
  return {
    type: "cashedOut",
    betId: options.betId,
    multiplier: multiplier(options.multiplier),
    payout: money(options.payout),
    creditStatus: options.creditStatus ?? "credited",
    balance: options.balance === null ? null : money(options.balance),
    auto: options.auto ?? false,
  };
}

export const betSettled = (
  betId: string,
  roundId: string,
): EventOf<"betSettled"> => ({
  type: "betSettled",
  betId,
  roundId,
  status: "lost",
});

export function betRefunded(options: {
  readonly betId: string;
  readonly roundId: string;
  readonly refunded: string;
  readonly balance: string | null;
}): EventOf<"betRefunded"> {
  return {
    type: "betRefunded",
    betId: options.betId,
    roundId: options.roundId,
    refunded: money(options.refunded),
    refundStatus: options.balance === null ? "pending" : "refunded",
    balance: options.balance === null ? null : money(options.balance),
  };
}

export const balanceUpdated = (balance: string): EventOf<"balanceUpdated"> => ({
  type: "balanceUpdated",
  balance: money(balance),
  currency: "BRL",
});

export const roundBetting = (roundId: string): BetsEvent => ({
  type: "roundBetting",
  roundId,
});
export const roundFlying = (roundId: string): BetsEvent => ({
  type: "roundFlying",
  roundId,
});
export const roundCrashed = (roundId: string): BetsEvent => ({
  type: "roundCrashed",
  roundId,
});
export const roundAnnulled = (roundId: string): BetsEvent => ({
  type: "roundAnnulled",
  roundId,
});

/** "round-41" → 41 (the ids of tests/frontend/pipa/support/rounds.ts). */
function roundNumberOf(roundId: string): number {
  const number = Number(roundId.replace(/\D/gu, ""));
  return Number.isInteger(number) ? number : 1;
}
