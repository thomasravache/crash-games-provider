// fetch double of the player wallet (SPEC-0021@4) and of the open bets read back on reconnection (SPEC-0025@1):
//   GET  /api/v1/wallet/balance        → 200 { balance, currency, mode }
//   POST /api/v1/wallet/demo/recharge  → 200 { balance, currency } | 409 RECHARGE_NOT_ALLOWED
//   GET  /api/v1/me/bets?limit=n       → 200 { items, nextCursor } | 404 while SPEC-0025 is not delivered
import type { MyBet, MyBetsPage } from "@apogeu/game-sdk";

export interface WalletRequest {
  readonly method: string;
  readonly path: string;
  readonly url: string;
  readonly authorization: string | null;
}

const json = (
  status: number,
  body: unknown,
  contentType = "application/json",
): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": contentType },
  });

const problem = (status: number, code: string): Response =>
  json(
    status,
    { type: "about:blank", title: code, status, code },
    "application/problem+json",
  );

export class FakeWallet {
  readonly requests: WalletRequest[] = [];
  /** Balance the server answers (decimal string). */
  balance: string;
  readonly currency = "BRL";
  /** Open and recent bets of GET /api/v1/me/bets; null: the endpoint answers 404 (SPEC-0025 not delivered). */
  myBets: MyBet[] | null = null;
  /** Balance after a demo recharge; null: the recharge answers 409 RECHARGE_NOT_ALLOWED. */
  rechargeTo: string | null = null;

  constructor(balance: string) {
    this.balance = balance;
  }

  readonly fetch = (input: string, init?: RequestInit): Promise<Response> => {
    const url = new URL(input);
    const method = (init?.method ?? "GET").toUpperCase();
    this.requests.push({
      method,
      path: url.pathname,
      url: input,
      authorization: new Headers(init?.headers).get("authorization"),
    });
    if (method === "GET" && url.pathname === "/api/v1/wallet/balance") {
      return Promise.resolve(
        json(200, {
          balance: this.balance,
          currency: this.currency,
          mode: "demo",
        }),
      );
    }
    if (method === "POST" && url.pathname === "/api/v1/wallet/demo/recharge") {
      if (this.rechargeTo === null)
        return Promise.resolve(problem(409, "RECHARGE_NOT_ALLOWED"));
      this.balance = this.rechargeTo;
      return Promise.resolve(
        json(200, { balance: this.balance, currency: this.currency }),
      );
    }
    if (method === "GET" && url.pathname === "/api/v1/me/bets") {
      if (this.myBets === null)
        return Promise.resolve(problem(404, "NOT_FOUND"));
      const page: MyBetsPage = { items: this.myBets, nextCursor: null };
      return Promise.resolve(json(200, page));
    }
    return Promise.resolve(new Response(null, { status: 404 }));
  };

  requestsTo(path: string): WalletRequest[] {
    return this.requests.filter((request) => request.path === path);
  }
}

/** An item of GET /api/v1/me/bets (SPEC-0025@1). */
export function myBet(options: {
  readonly betId: string;
  readonly panel: 1 | 2;
  readonly roundId: string | null;
  readonly amount: string;
  readonly status: MyBet["status"];
}): MyBet {
  return {
    betId: options.betId,
    roundId: options.roundId,
    roundNumber: null,
    panel: options.panel,
    amount: options.amount as MyBet["amount"],
    currency: "BRL",
    autoCashOut: null,
    multiplier: null,
    payout: null,
    status: options.status,
    placedAt: "2026-10-11T12:00:00.000Z",
  };
}
