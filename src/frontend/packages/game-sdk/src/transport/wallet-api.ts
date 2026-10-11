// Player wallet REST (SPEC-0021@4) and the open bets read back on reconnection (SPEC-0025@1), with the player session
// (SPEC-0009@1) as an HTTP bearer:
//   GET  /api/v1/wallet/balance        → 200 { balance, currency, mode }
//   POST /api/v1/wallet/demo/recharge  → 200 { balance, currency } | 409 RECHARGE_NOT_ALLOWED | 403 NOT_DEMO
//   GET  /api/v1/me/bets?limit=n       → 200 { items, nextCursor } | 404 while SPEC-0025 is not delivered

import { notImplemented } from "../not-implemented";
import type { FetchLike } from "./session-api";
import type { MyBetsPage, WalletBalance, WalletRecharge } from "./wire";

/** A wallet call failed: the ProblemDetails `code` (RECHARGE_NOT_ALLOWED, NOT_DEMO, …), NETWORK or UNKNOWN. */
export class WalletError extends Error {
  readonly code: string;

  constructor(code: string) {
    super(code);
    this.name = "WalletError";
    this.code = code;
  }
}

export function fetchWalletBalance(
  apiBaseUrl: string,
  sessionToken: string,
  fetchImpl: FetchLike,
): Promise<WalletBalance> {
  return notImplemented(apiBaseUrl, sessionToken, fetchImpl);
}

export function rechargeDemoWallet(
  apiBaseUrl: string,
  sessionToken: string,
  fetchImpl: FetchLike,
): Promise<WalletRecharge> {
  return notImplemented(apiBaseUrl, sessionToken, fetchImpl);
}

/** Most recent bets of the player; resolves null when the endpoint answers 404 (not delivered yet). */
export function fetchMyBets(
  apiBaseUrl: string,
  sessionToken: string,
  fetchImpl: FetchLike,
  limit?: number,
): Promise<MyBetsPage | null> {
  return notImplemented(apiBaseUrl, sessionToken, fetchImpl, limit);
}
