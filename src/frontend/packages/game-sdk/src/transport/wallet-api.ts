// Player wallet REST (SPEC-0021@4) and the open bets read back on reconnection (SPEC-0025@1), with the player session
// (SPEC-0009@1) as an HTTP bearer:
//   GET  /api/v1/wallet/balance        → 200 { balance, currency, mode }
//   POST /api/v1/wallet/demo/recharge  → 200 { balance, currency } | 409 RECHARGE_NOT_ALLOWED | 403 NOT_DEMO
//   GET  /api/v1/me/bets?limit=n&before=<cursor> → 200 { items, nextCursor } | 400 INVALID_CURSOR | 404 while SPEC-0025
//                                                  is not delivered

import { problemCode, type FetchLike } from "./session-api";
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

function authorized(sessionToken: string): Record<string, string> {
  return { accept: "application/json", authorization: `Bearer ${sessionToken}` };
}

async function call(fetchImpl: FetchLike, url: string, init: RequestInit): Promise<Response> {
  try {
    return await fetchImpl(url, init);
  } catch {
    throw new WalletError("NETWORK");
  }
}

function isRecord(body: unknown): body is Record<string, unknown> {
  return typeof body === "object" && body !== null;
}

async function balanceBody<T extends { balance: unknown; currency: unknown }>(
  response: Response,
): Promise<T> {
  if (!response.ok) throw new WalletError((await problemCode(response)) ?? "UNKNOWN");
  const body = (await response.json()) as unknown;
  if (
    !isRecord(body) ||
    typeof body["balance"] !== "string" ||
    typeof body["currency"] !== "string"
  ) {
    throw new WalletError("UNKNOWN");
  }
  return body as T;
}

export async function fetchWalletBalance(
  apiBaseUrl: string,
  sessionToken: string,
  fetchImpl: FetchLike,
): Promise<WalletBalance> {
  const response = await call(fetchImpl, `${apiBaseUrl}/api/v1/wallet/balance`, {
    headers: authorized(sessionToken),
  });
  return balanceBody<WalletBalance>(response);
}

export async function rechargeDemoWallet(
  apiBaseUrl: string,
  sessionToken: string,
  fetchImpl: FetchLike,
): Promise<WalletRecharge> {
  const response = await call(fetchImpl, `${apiBaseUrl}/api/v1/wallet/demo/recharge`, {
    method: "POST",
    headers: authorized(sessionToken),
  });
  return balanceBody<WalletRecharge>(response);
}

/**
 * Most recent bets of the player (the page after the opaque cursor `before`, when given); resolves null when the
 * endpoint answers 404 (not delivered yet).
 */
export async function fetchMyBets(
  apiBaseUrl: string,
  sessionToken: string,
  fetchImpl: FetchLike,
  limit?: number,
  before?: string,
): Promise<MyBetsPage | null> {
  const params = new URLSearchParams();
  if (limit !== undefined) params.set("limit", String(limit));
  if (before !== undefined) params.set("before", before);
  const search = params.toString();
  const query = search === "" ? "" : `?${search}`;
  const response = await call(fetchImpl, `${apiBaseUrl}/api/v1/me/bets${query}`, {
    headers: authorized(sessionToken),
  });
  if (response.status === 404) return null;
  if (!response.ok) throw new WalletError((await problemCode(response)) ?? "UNKNOWN");
  const body = (await response.json()) as unknown;
  if (!isRecord(body) || !Array.isArray(body["items"])) throw new WalletError("UNKNOWN");
  return body as unknown as MyBetsPage;
}
