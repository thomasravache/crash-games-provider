// Fixtures and rig of SPEC-0031 shaped like the contract it consumes (SPEC-0025@4): the JoinSalaBets snapshot, the
// LiveBetsBatch messages and the pages of GET /api/v1/me/bets. The real bets client of game-sdk runs over the fake
// BetsHub of SPEC-0026, a real round store and a fetch double that pages the history by the `before` cursor.
import {
  createBetsClient,
  type BetsClient,
  type LiveBetsBatch,
  type LiveBetsSnapshot,
} from "@apogeu/game-sdk";
import {
  createRoundStore,
  type LiveBetsState,
  type Money,
  type MyBetEntry,
  type MyBetsState,
  type Multiplier,
  type RoundEvent,
  type RoundStore,
} from "@apogeu/game-sdk/state";
import { vi } from "vitest";

import { apiBaseUrl, limits, sessionToken } from "../../bets/support/bets";
import { FakeBetsHub } from "../../bets/support/fake-bets-hub";

export { sessionToken };

export const money = (value: string): Money => value as Money;
export const multiplier = (value: string): Multiplier => value as Multiplier;

type LiveBetWire = LiveBetsSnapshot["bets"][number];

/** A bet of the sala as the server sends it (nickname already anonymized by the server). */
export function liveBet(
  betRef: string,
  nickname: string,
  amount: string,
  cashOut?: { readonly multiplier: string; readonly payout: string },
): LiveBetWire {
  return {
    betRef,
    nickname,
    amount: money(amount),
    currency: "BRL",
    multiplier: cashOut === undefined ? null : multiplier(cashOut.multiplier),
    payout: cashOut === undefined ? null : money(cashOut.payout),
  };
}

export function liveSnapshot(
  roundId: string,
  bets: readonly LiveBetWire[],
  totals: { totalBets: number; totalPlayers: number; totalWagered: string },
): LiveBetsSnapshot {
  return {
    roundId,
    bets: [...bets],
    totalBets: totals.totalBets,
    totalPlayers: totals.totalPlayers,
    totalWagered: money(totals.totalWagered),
  };
}

export function liveBatch(
  roundId: string,
  placed: readonly LiveBetWire[],
  cashedOut: readonly {
    betRef: string;
    multiplier: string;
    payout: string;
  }[],
  totals: { totalBets: number; totalPlayers: number; totalWagered: string },
): LiveBetsBatch {
  return {
    roundId,
    placed: [...placed],
    cashedOut: cashedOut.map((entry) => ({
      betRef: entry.betRef,
      multiplier: multiplier(entry.multiplier),
      payout: money(entry.payout),
    })),
    totalBets: totals.totalBets,
    totalPlayers: totals.totalPlayers,
    totalWagered: money(totals.totalWagered),
  };
}

/** An item of GET /api/v1/me/bets (MyBetItem of SPEC-0025@4). */
export function myBet(options: {
  readonly betId: string;
  readonly roundNumber: number;
  readonly amount: string;
  readonly status: MyBetEntry["status"];
  readonly multiplier?: string;
  readonly payout?: string;
  readonly placedAt: string;
}): MyBetEntry {
  return {
    betId: options.betId,
    roundId: `round-${String(options.roundNumber)}`,
    roundNumber: options.roundNumber,
    panel: 1,
    amount: money(options.amount),
    currency: "BRL",
    autoCashOut: null,
    multiplier:
      options.multiplier === undefined ? null : multiplier(options.multiplier),
    payout: options.payout === undefined ? null : money(options.payout),
    status: options.status,
    placedAt: options.placedAt,
  };
}

export interface HistoryRequest {
  readonly url: URL;
  readonly authorization: string | null;
}

/**
 * fetch double of the wallet balance and of GET /api/v1/me/bets paged by cursor: no `before` answers `pages[0]`,
 * `before=<cursor>` answers the page registered for that cursor; an unknown cursor answers 400 INVALID_CURSOR.
 * Each answer can be held until the test releases it (concurrent "carregar mais").
 */
export class HistoryServer {
  readonly requests: HistoryRequest[] = [];
  private readonly pages = new Map<
    string,
    { items: MyBetEntry[]; nextCursor: string | null }
  >();
  private held: (() => void)[] = [];
  hold = false;

  page(
    before: string | null,
    items: readonly MyBetEntry[],
    nextCursor: string | null,
  ): void {
    this.pages.set(before ?? "", { items: [...items], nextCursor });
  }

  /** Releases the answers held while `hold` was on. */
  release(): void {
    const held = this.held;
    this.held = [];
    for (const resume of held) resume();
  }

  readonly fetch = async (
    input: string,
    init?: RequestInit,
  ): Promise<Response> => {
    const url = new URL(input);
    if (url.pathname === "/api/v1/wallet/balance") {
      return json(200, { balance: "3000.00", currency: "BRL", mode: "demo" });
    }
    if (url.pathname !== "/api/v1/me/bets")
      return new Response(null, { status: 404 });
    this.requests.push({
      url,
      authorization: new Headers(init?.headers).get("authorization"),
    });
    if (this.hold)
      await new Promise<void>((resolve) => this.held.push(resolve));
    const page = this.pages.get(url.searchParams.get("before") ?? "");
    if (page === undefined) {
      return json(
        400,
        {
          type: "about:blank",
          title: "INVALID_CURSOR",
          status: 400,
          code: "INVALID_CURSOR",
        },
        "application/problem+json",
      );
    }
    return json(200, page);
  };
}

function json(
  status: number,
  body: unknown,
  contentType = "application/json",
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": contentType },
  });
}

export interface TabsRig {
  readonly client: BetsClient;
  readonly hub: FakeBetsHub;
  readonly history: HistoryServer;
  readonly rounds: RoundStore;
  live(): LiveBetsState;
  mine(): MyBetsState;
  round(event: RoundEvent): void;
  /** Waits until `probe` holds on the live bets store (async hub answers). */
  untilLive(probe: (state: LiveBetsState) => boolean): Promise<void>;
}

/** The bets client over the doubles; `snapshot` is what JoinSalaBets answers. */
export function newTabsRig(snapshot?: LiveBetsSnapshot): TabsRig {
  const hub = new FakeBetsHub(limits);
  if (snapshot !== undefined) {
    hub.respond("JoinSalaBets", () =>
      Promise.resolve(structuredClone(snapshot)),
    );
  }
  const history = new HistoryServer();
  const rounds = createRoundStore();
  const client = createBetsClient({
    apiBaseUrl,
    rounds,
    fetch: history.fetch,
    connectHub: () => hub,
  });
  return {
    client,
    hub,
    history,
    rounds,
    live: () => client.liveBets.getState(),
    mine: () => client.myBets.getState(),
    round: (event) => {
      rounds.dispatch(event);
    },
    untilLive: async (probe) => {
      await vi.waitFor(
        () => {
          if (!probe(client.liveBets.getState()))
            throw new Error("live bets store not there yet");
        },
        { timeout: 1_000, interval: 5 },
      );
    },
  };
}
