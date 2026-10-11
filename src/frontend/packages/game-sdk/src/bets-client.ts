// Bets client (SPEC-0026 §6): connects to /hubs/bets with the session token, reads the limits and the wallet balance,
// sends the panel intents (PlaceBet, CancelBet, CashOut, demo recharge) and maps every server message to a bets store
// event. The store only shows what the server confirmed (no optimistic balance). After a reconnection the balance and
// the open bets are read again from the server (UT-03); GET /api/v1/me/bets answering 404 keeps the local panels.

import type { Money, Multiplier } from "@apogeu/contracts";

import { toBetErrorCode, type BetErrorCode } from "./state/bet-errors";
import type { OpenBet, PanelNumber } from "./state/bets-state";
import { createBetsStore, panelOf, type BetsStore } from "./state/bets-store";
import { normalizeMoney } from "./state/money";
import type { RoundState, RoundStateSource } from "./state/round-state";
import {
  createSignalRBetsConnection,
  hubErrorCode,
  type BetsHubConnection,
  type ConnectBetsHub,
} from "./transport/bets-hub-connection";
import type { FetchLike } from "./transport/session-api";
import {
  fetchMyBets,
  fetchWalletBalance,
  rechargeDemoWallet,
  WalletError,
} from "./transport/wallet-api";
import type { MyBet } from "./transport/wire";

export interface BetsClientOptions {
  /** Base URL of the API (Host), without a trailing slash; the hub is `${apiBaseUrl}/hubs/bets`. */
  readonly apiBaseUrl: string;
  /** Round state the panels follow (placed → active on the flight, finished panels back to idle on the next betting). */
  readonly rounds: RoundStateSource;
  /** HTTP client; defaults to the global fetch. */
  readonly fetch?: FetchLike;
  /** Hub connection factory; defaults to createSignalRBetsConnection. */
  readonly connectHub?: ConnectBetsHub;
  /** clientBetId (uuid) generator; defaults to crypto.randomUUID. */
  readonly newClientBetId?: () => string;
}

/** What the player asks for when betting on a panel; values travel as decimal strings. */
export interface PlaceBetIntent {
  readonly amount: Money;
  /** Automatic cash-out target (≥ 1.01), or null without it. */
  readonly autoCashOut: Multiplier | null;
}

/** A bet request failed: the server code (or NETWORK/UNKNOWN); the same code is recorded in the panel state. */
export class BetError extends Error {
  readonly code: BetErrorCode;

  constructor(code: BetErrorCode) {
    super(code);
    this.name = "BetError";
    this.code = code;
  }
}

export interface BetsClient {
  readonly store: BetsStore;
  /**
   * Connects to /hubs/bets with the session token (memory only), subscribes to the server messages, reads the limits
   * (GetBetLimits) and the balance (GET /api/v1/wallet/balance) and starts following the round state.
   */
  start(sessionToken: string): Promise<void>;
  /** PlaceBet on the panel; rejects with BetError (the panel shows the message). */
  placeBet(panel: PanelNumber, intent: PlaceBetIntent): Promise<void>;
  /** CancelBet of the panel's placed or queued bet; rejects with BetError. */
  cancelBet(panel: PanelNumber): Promise<void>;
  /** CashOut of the panel's active bet (CashedOut or CashOutPending); rejects with BetError. */
  cashOut(panel: PanelNumber): Promise<void>;
  /** POST /api/v1/wallet/demo/recharge and the new balance; rejects with BetError. */
  rechargeDemo(): Promise<void>;
  /** Reconnection reload: balance from the wallet, open bets from GET /api/v1/me/bets (404 keeps the panels). */
  reload(): Promise<void>;
  stop(): Promise<void>;
}

/** Bets read back on reconnection (the most recent ones hold every open bet of the two panels). */
const reloadLimit = 20;

/** The error code of a failed hub invocation or wallet call (NETWORK when the transport failed without a code). */
function errorCode(error: unknown): BetErrorCode {
  if (error instanceof BetError) return error.code;
  if (error instanceof WalletError) return toBetErrorCode(error.code);
  const code = hubErrorCode(error);
  return code === undefined ? "NETWORK" : toBetErrorCode(code);
}

function isPanelNumber(value: number): value is PanelNumber {
  return value === 1 || value === 2;
}

/** The open bets (placed, queued, active) of GET /api/v1/me/bets, one per panel (the most recent first). */
function openBetsOf(items: readonly MyBet[]): OpenBet[] {
  const open: OpenBet[] = [];
  for (const item of items) {
    if (item.status !== "placed" && item.status !== "queued" && item.status !== "active") continue;
    const panel = item.panel;
    if (!isPanelNumber(panel) || open.some((bet) => bet.panel === panel)) continue;
    open.push({
      betId: item.betId,
      roundId: item.roundId,
      panel,
      amount: item.amount,
      autoCashOut: item.autoCashOut,
      status: item.status,
    });
  }
  return open;
}

export function createBetsClient(options: BetsClientOptions): BetsClient {
  const store = createBetsStore();
  const apiBaseUrl = options.apiBaseUrl.replace(/\/+$/u, "");
  const fetchImpl: FetchLike = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  const connectHub = options.connectHub ?? createSignalRBetsConnection;
  const newClientBetId = options.newClientBetId ?? (() => globalThis.crypto.randomUUID());

  let connection: BetsHubConnection | undefined;
  // The session token lives only in this closure (memory), never in storage (SPEC-0026 §5).
  let sessionToken: string | undefined;
  let stopped = false;
  let unfollowRounds: (() => void) | undefined;
  let lastRound: Pick<RoundState, "phase" | "roundId"> | undefined;

  function hub(): BetsHubConnection {
    if (connection === undefined) throw new BetError("NETWORK");
    return connection;
  }

  function token(): string {
    if (sessionToken === undefined) throw new BetError("NETWORK");
    return sessionToken;
  }

  /** Mirrors the round phases into the bets store (each transition once). */
  function followRound(round: RoundState): void {
    if (round.roundId === null) return;
    if (lastRound?.roundId === round.roundId && lastRound.phase === round.phase) return;
    lastRound = { phase: round.phase, roundId: round.roundId };
    switch (round.phase) {
      case "betting":
        store.dispatch({ type: "roundBetting", roundId: round.roundId });
        return;
      case "flying":
        store.dispatch({ type: "roundFlying", roundId: round.roundId });
        return;
      case "crashed":
        store.dispatch({ type: "roundCrashed", roundId: round.roundId });
        return;
      case "annulled":
        store.dispatch({ type: "roundAnnulled", roundId: round.roundId });
        return;
      case "idle":
        return;
    }
  }

  /** A placed bet whose round already took off (the flight message arrived before the PlaceBet result). */
  function catchUpWithRound(): void {
    const round = options.rounds.getState();
    if (round.phase === "flying" && round.roundId !== null) {
      store.dispatch({ type: "roundFlying", roundId: round.roundId });
    }
  }

  function listen(connected: BetsHubConnection): void {
    connected.on("BetActivated", (message) => {
      store.dispatch({ type: "betActivated", ...message });
      catchUpWithRound();
    });
    connected.on("CashedOut", (message) => {
      store.dispatch({ type: "cashedOut", ...message });
    });
    connected.on("CashOutRejected", (message) => {
      store.dispatch({ type: "cashOutRejected", ...message });
    });
    connected.on("BalanceUpdated", (message) => {
      store.dispatch({ type: "balanceUpdated", ...message });
    });
    connected.on("BetSettled", (message) => {
      store.dispatch({ type: "betSettled", ...message });
    });
    connected.on("BetRefunded", (message) => {
      store.dispatch({ type: "betRefunded", ...message });
    });
    connected.onreconnected(() => {
      if (stopped) return;
      // Messages may have been lost while offline: the server is read again (UT-03).
      reload().catch(() => undefined);
    });
  }

  async function readBalance(): Promise<void> {
    const wallet = await fetchWalletBalance(apiBaseUrl, token(), fetchImpl);
    store.dispatch({ type: "balance", balance: wallet.balance, currency: wallet.currency });
  }

  async function reload(): Promise<void> {
    const session = token();
    const [wallet, page] = await Promise.all([
      fetchWalletBalance(apiBaseUrl, session, fetchImpl),
      // Until SPEC-0025 delivers the endpoint (404 → null) or on any failure, the local panels are kept.
      fetchMyBets(apiBaseUrl, session, fetchImpl, reloadLimit).catch(() => null),
    ]);
    if (stopped) return;
    store.dispatch({
      type: "reloaded",
      balance: wallet.balance,
      currency: wallet.currency,
      openBets: page === null ? null : openBetsOf(page.items),
    });
    lastRound = undefined;
    followRound(options.rounds.getState());
  }

  /** Runs a panel request; a failure is recorded in the panel and rethrown as BetError. */
  async function request(panel: PanelNumber, run: () => Promise<void>): Promise<void> {
    try {
      await run();
    } catch (error: unknown) {
      const code = errorCode(error);
      store.dispatch({ type: "requestFailed", panel, code });
      throw new BetError(code);
    }
  }

  return {
    store,
    async start(session) {
      stopped = false;
      sessionToken = session;
      const connected = connectHub(`${apiBaseUrl}/hubs/bets`, {
        accessTokenFactory: () => session,
      });
      connection = connected;
      listen(connected);
      await connected.start();
      const limits = await connected.invoke("GetBetLimits");
      store.dispatch({ type: "limits", limits });
      await readBalance();
      unfollowRounds?.();
      unfollowRounds = options.rounds.subscribe(() => {
        followRound(options.rounds.getState());
      });
      followRound(options.rounds.getState());
    },
    placeBet(panel, intent) {
      return request(panel, async () => {
        const clientBetId = newClientBetId();
        const amount = normalizeMoney(intent.amount);
        store.dispatch({
          type: "placeRequested",
          panel,
          clientBetId,
          amount,
          autoCashOut: intent.autoCashOut,
        });
        const accepted = await hub().invoke("PlaceBet", {
          clientBetId,
          panel,
          amount,
          autoCashOut: intent.autoCashOut,
        });
        if (!isPanelNumber(accepted.panel)) throw new BetError("INVALID_PANEL");
        store.dispatch({ type: "betAccepted", ...accepted, panel: accepted.panel });
        catchUpWithRound();
      });
    },
    cancelBet(panel) {
      return request(panel, async () => {
        const betId = panelOf(store.getState(), panel).betId;
        if (betId === null) throw new BetError("BET_NOT_FOUND");
        store.dispatch({ type: "cancelRequested", panel });
        const cancelled = await hub().invoke("CancelBet", { betId });
        store.dispatch({ type: "betCancelled", ...cancelled });
      });
    },
    cashOut(panel) {
      return request(panel, async () => {
        const betId = panelOf(store.getState(), panel).betId;
        if (betId === null) throw new BetError("BET_NOT_ACTIVE");
        store.dispatch({ type: "cashOutRequested", panel });
        const result = await hub().invoke("CashOut", { betId });
        if (result.cashedOut !== null) store.dispatch({ type: "cashedOut", ...result.cashedOut });
        else if (result.pending !== null)
          store.dispatch({ type: "cashOutPending", ...result.pending });
      });
    },
    async rechargeDemo() {
      try {
        const recharge = await rechargeDemoWallet(apiBaseUrl, token(), fetchImpl);
        store.dispatch({ type: "balance", balance: recharge.balance, currency: recharge.currency });
      } catch (error: unknown) {
        throw new BetError(errorCode(error));
      }
    },
    reload,
    async stop() {
      stopped = true;
      unfollowRounds?.();
      unfollowRounds = undefined;
      lastRound = undefined;
      const connected = connection;
      connection = undefined;
      if (connected !== undefined) await connected.stop();
    },
  };
}
