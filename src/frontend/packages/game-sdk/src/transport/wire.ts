// Wire types of the RoundsHub (SPEC-0008@2 §6), of the session exchange (SPEC-0009@1 §6) and of the BetsHub and wallet
// (SPEC-0026), taken from the generated contracts (ADR-0008): never written by hand here, so a contract change breaks
// the build of the client. The SPEC-0025 history types are local stand-ins until that endpoint is generated.

import type {
  BetsHubClient,
  BetsHubServer,
  components,
  Money,
  Multiplier,
  RoundsHubServer,
} from "@apogeu/contracts";

export type {
  BetsHubClient,
  BetsHubServer,
  Money,
  Multiplier,
  RoundsHubClient,
  RoundsHubServer,
} from "@apogeu/contracts";

/** Result of JoinSala: the sala, the current round and the last 20 crashes. */
export type RoundSnapshot = Awaited<ReturnType<RoundsHubServer["JoinSala"]>>;

/** POST /api/v1/sessions request body (SPEC-0009@1). */
export type SessionRequest = components["schemas"]["CreateSessionRequest"];

/** POST /api/v1/sessions 200 body (SPEC-0009@1). */
export type SessionResponse = components["schemas"]["SessionResponse"];

/** Error codes of POST /api/v1/sessions (ProblemDetails `code`). */
export type SessionErrorCode =
  "LAUNCH_TOKEN_INVALID" | "LAUNCH_TOKEN_EXPIRED" | "LAUNCH_TOKEN_USED" | "RATE_LIMITED";

// ---- BetsHub (SPEC-0022@3, SPEC-0023@2, SPEC-0024@2) and wallet REST (SPEC-0021@4), used by the bets client
// (SPEC-0026). Taken from the generated contracts, except where marked as a local stand-in.

/** PlaceBet request (SPEC-0022@3). */
export type PlaceBetRequest = Parameters<BetsHubServer["PlaceBet"]>[0];

/** PlaceBet result (SPEC-0022@3). */
export type BetAccepted = Awaited<ReturnType<BetsHubServer["PlaceBet"]>>;

/** CancelBet result (SPEC-0022@3). */
export type BetCancelled = Awaited<ReturnType<BetsHubServer["CancelBet"]>>;

/** CashOut result envelope: exactly one of cashedOut / pending (SPEC-0023@2). */
export type CashOutResult = Awaited<ReturnType<BetsHubServer["CashOut"]>>;

/** CashedOut, as the CashOut result and as the server message (SPEC-0023@2). */
export type CashedOut = Parameters<BetsHubClient["CashedOut"]>[0];

/** CashOutPending (SPEC-0023@2). */
export type CashOutPending = NonNullable<CashOutResult["pending"]>;

/** CashOutRejected (SPEC-0023@2). */
export type CashOutRejected = Parameters<BetsHubClient["CashOutRejected"]>[0];

/** BalanceUpdated (SPEC-0023@2). */
export type BalanceUpdated = Parameters<BetsHubClient["BalanceUpdated"]>[0];

/** BetActivated (SPEC-0022@3). */
export type BetActivated = Parameters<BetsHubClient["BetActivated"]>[0];

/** GetBetLimits result (SPEC-0020@2 / SPEC-0022@3). */
export type BetLimitsResponse = Awaited<ReturnType<BetsHubServer["GetBetLimits"]>>;

/** BetSettled (SPEC-0024@2). */
export type BetSettled = Parameters<BetsHubClient["BetSettled"]>[0];

/** BetRefunded (SPEC-0024@2); balance null while refundStatus is "pending". */
export type BetRefunded = Parameters<BetsHubClient["BetRefunded"]>[0];

/** GET /api/v1/wallet/balance 200 body (SPEC-0021@4). */
export type WalletBalance = components["schemas"]["BalanceResponse"];

/** POST /api/v1/wallet/demo/recharge 200 body (SPEC-0021@4). */
export type WalletRecharge = components["schemas"]["RechargeResponse"];

/** Item of GET /api/v1/me/bets (SPEC-0025@1). LOCAL STAND-IN: the endpoint is not in the generated contracts yet. */
export interface MyBet {
  betId: string;
  roundId: string | null;
  roundNumber: number | null;
  panel: number;
  amount: Money;
  currency: string;
  autoCashOut: Multiplier | null;
  multiplier: Multiplier | null;
  payout: Money | null;
  status: "placed" | "queued" | "active" | "cashed_out" | "lost" | "cancelled" | "refunded";
  placedAt: string;
}

/** GET /api/v1/me/bets 200 body (SPEC-0025@1). LOCAL STAND-IN, like MyBet. */
export interface MyBetsPage {
  items: MyBet[];
  nextCursor: string | null;
}

// ---- Live bets and history (SPEC-0025@4), consumed by the bets tabs (SPEC-0031). Taken from the generated contracts.

/** JoinSalaBets result: the bets of the current round, up to 100, largest first (SPEC-0025@4). */
export type LiveBetsSnapshot = Awaited<ReturnType<BetsHubServer["JoinSalaBets"]>>;

/** LiveBetsBatch, sent to the sala every 200 ms when there is news (SPEC-0025@4). */
export type LiveBetsBatch = Parameters<BetsHubClient["LiveBetsBatch"]>[0];
