import {
  BetError,
  createBetsClient,
  createGameClient,
  createSignalRBetsConnection,
  createSignalRConnection,
  type BetsClient,
  type GameClient,
} from "@apogeu/game-sdk";
import type {
  Money,
  PanelState,
  RoundState,
  RoundStateSource,
} from "@apogeu/game-sdk/state";
import { describe, expect, test, vi } from "vitest";

import {
  betsHubSchema,
  BetsHubRecorder,
  type BetsHubRecord,
} from "../bets/support/bets-recording";
import { exportHubManifest, openApiSchema } from "../support/contracts";
import { apiBaseUrl, launchDemo, waitUntil } from "../support/environment";
import { schemaErrors } from "../support/json-schema";
import { HttpRecorder } from "../support/recording";

// SPEC-0031 CT-01 (Emenda v1): the real game-sdk in Node against the Host started by Aspire. A demo player joins the
// live bets of the sala (JoinSalaBets → LiveBetsSnapshot), bets R$ 10,00, receives the LiveBetsBatch messages of the
// round (placed and, when the cash out lands, cashedOut) and reads its history (GET /api/v1/me/bets, "carregar mais"
// while there is a cursor). Every recorded snapshot, batch and history page validates against the generated contracts
// of SPEC-0025@4: the hub manifest of BetsHub and the OpenAPI document.
//
// Run: APOGEU_API_URL=<base URL of the api resource> pnpm -C src/frontend test:integration
// (Aspire up with `dotnet run --project src/backend/AppHost`; the hub manifest also needs `dotnet build Apogeu.slnx`).

/** Betting 6 s + flight up to the cap (≈ 154 s) + pause 3 s, with margin (SPEC-0008@2). */
const roundTimeoutMs = 190_000;
const flowTimeoutMs = 3 * roundTimeoutMs;
const stake = "10.00";

interface LiveFlow {
  readonly api: string;
  readonly http: HttpRecorder;
  readonly hub: BetsHubRecorder;
  /** Panel 1 after the outcome of the bet. */
  readonly outcome: PanelState;
}

let liveFlow: Promise<LiveFlow> | undefined;

function followLiveFlow(): Promise<LiveFlow> {
  liveFlow ??= runLiveFlow();
  return liveFlow;
}

/** Waits until the recorder holds a record accepted by `probe` (the hub answers asynchronously). */
async function recorded(
  hub: BetsHubRecorder,
  probe: (record: BetsHubRecord) => boolean,
  what: string,
  timeout: number,
): Promise<BetsHubRecord> {
  let found: BetsHubRecord | undefined;
  await vi.waitFor(
    () => {
      found = hub.records.find(probe);
      if (found === undefined) throw new Error(`nothing recorded: ${what}`);
    },
    { timeout, interval: 50 },
  );
  if (found === undefined) throw new Error(`nothing recorded: ${what}`);
  return found;
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null;

const placedCount = (record: BetsHubRecord): number =>
  isRecord(record.payload) && Array.isArray(record.payload["placed"])
    ? record.payload["placed"].length
    : 0;

async function runLiveFlow(): Promise<LiveFlow> {
  const api = apiBaseUrl();
  const launch = await launchDemo(api);
  if (launch.status !== 302 || launch.launchToken === "") {
    throw new Error(
      `GET ${api}/dev/launch-demo must answer 302 with a launch token; got ${String(launch.status)}`,
    );
  }

  const http = new HttpRecorder();
  const hub = new BetsHubRecorder();
  let game: GameClient | undefined;
  const roundStore = (): RoundStateSource => {
    if (game === undefined)
      throw new Error("the game client is not created yet");
    return game.store;
  };
  const rounds: RoundStateSource = {
    getState: (): RoundState => roundStore().getState(),
    subscribe: (listener) => roundStore().subscribe(listener),
  };
  const bets: BetsClient = createBetsClient({
    apiBaseUrl: api,
    rounds,
    fetch: http.fetch,
    connectHub: (hubUrl, options) =>
      hub.wrap(createSignalRBetsConnection(hubUrl, options)),
  });
  game = createGameClient({
    apiBaseUrl: api,
    fetch: http.fetch,
    connectHub: createSignalRConnection,
    bets,
  });
  const gameStore = game.store;
  const onRounds = (listener: () => void): (() => void) =>
    gameStore.subscribe(listener);
  const onBets = (listener: () => void): (() => void) =>
    bets.store.subscribe(listener);
  const panel1 = (): PanelState => bets.store.getState().panels[0];

  try {
    await game.start(launch.launchToken);
    // The tabs join the live bets of the sala as soon as the client starts.
    await recorded(
      hub,
      (record) => record.name === "JoinSalaBets" && record.kind === "result",
      "the JoinSalaBets snapshot",
      30_000,
    );
    await waitUntil(
      onBets,
      () => (bets.store.getState().limits !== null ? true : undefined),
      30_000,
      "GetBetLimits",
    );

    await waitUntil(
      onRounds,
      () => (gameStore.getState().phase === "betting" ? true : undefined),
      roundTimeoutMs,
      "a round in betting",
    );
    await bets.placeBet(1, { amount: stake as Money, autoCashOut: null });
    // The bet reaches the sala in a LiveBetsBatch (every 200 ms when there is news).
    await recorded(
      hub,
      (record) =>
        record.name === "LiveBetsBatch" &&
        record.kind === "message" &&
        placedCount(record) > 0,
      "a LiveBetsBatch with the placed bet",
      10_000,
    );

    const flying = await waitUntil(
      onBets,
      () => {
        const status = panel1().status;
        return status === "active" || status === "lost" || status === "refunded"
          ? status
          : undefined;
      },
      2 * roundTimeoutMs,
      "panel 1 flying (or already finished)",
    );
    if (flying === "active") {
      await bets.cashOut(1).catch((error: unknown) => {
        // A cash out that arrives after the crash is a loss, not a failure of the test.
        if (!(error instanceof BetError)) throw error;
      });
    }
    const outcome = await waitUntil(
      onBets,
      () => {
        const panel = panel1();
        return panel.status === "cashed_out" ||
          panel.status === "lost" ||
          panel.status === "refunded"
          ? panel
          : undefined;
      },
      roundTimeoutMs,
      "the outcome of panel 1",
    );
    if (outcome.status === "cashed_out") {
      await recorded(
        hub,
        (record) =>
          record.name === "LiveBetsBatch" &&
          isRecord(record.payload) &&
          Array.isArray(record.payload["cashedOut"]) &&
          record.payload["cashedOut"].length > 0,
        "a LiveBetsBatch with the cash out",
        10_000,
      );
    }

    // "Minhas apostas": the first page, then "carregar mais" while there is a cursor (at most a few pages).
    await bets.loadMyBets();
    for (
      let pages = 1;
      pages < 5 && bets.myBets.getState().nextCursor !== null;
      pages += 1
    ) {
      await bets.loadMoreMyBets();
    }
    return { api, http, hub, outcome };
  } finally {
    await game.stop();
  }
}

describe("SPEC-0031 abas de apostas contra o backend real (Aspire)", () => {
  test(
    "SPEC-0031:CT-01 LiveBetsSnapshot, LiveBetsBatch e GET /api/v1/me/bets reais conferem com os contratos gerados (SPEC-0025@4)",
    async () => {
      const [{ api, http, hub, outcome }, manifest] = await Promise.all([
        followLiveFlow(),
        exportHubManifest(),
      ]);

      const live = hub.records.filter(
        (record) =>
          (record.name === "JoinSalaBets" && record.kind === "result") ||
          (record.name === "LiveBetsBatch" && record.kind === "message"),
      );
      const names = new Set(live.map((record) => record.name));
      expect(names).toContain("JoinSalaBets");
      expect(names).toContain("LiveBetsBatch");
      const violations = live.flatMap((record) =>
        schemaErrors(betsHubSchema(manifest, record), record.payload).map(
          (error) => `${record.name} (${record.kind}): ${error}`,
        ),
      );
      expect(violations).toEqual([]);

      // Money and multipliers travel as decimal strings; nicknames arrive anonymized.
      for (const record of live) {
        const payload = record.payload as Record<string, unknown>;
        expect(typeof payload["totalWagered"]).toBe("string");
        const rows = [
          ...((payload["bets"] as unknown[] | undefined) ?? []),
          ...((payload["placed"] as unknown[] | undefined) ?? []),
        ] as Record<string, unknown>[];
        for (const row of rows) {
          expect(typeof row["amount"]).toBe("string");
          expect(row["nickname"]).toMatch(/\*\*\*$/u);
        }
      }

      const pages = http.exchanges.filter(
        (exchange) =>
          exchange.method === "GET" &&
          exchange.url.startsWith(`${api}/api/v1/me/bets`),
      );
      expect(pages.length).toBeGreaterThanOrEqual(1);
      const schema = openApiSchema(
        "/api/v1/me/bets",
        "get",
        200,
        "application/json",
      );
      for (const exchange of pages) {
        expect(exchange.status).toBe(200);
        expect(schemaErrors(schema, exchange.responseBody)).toEqual([]);
      }
      // A page after the first travels with the cursor of the previous one.
      for (const [index, exchange] of pages.entries()) {
        if (index === 0) continue;
        const previous = pages[index - 1]?.responseBody as
          { nextCursor?: unknown } | undefined;
        expect(new URL(exchange.url).searchParams.get("before")).toBe(
          previous?.nextCursor,
        );
      }
      const items = pages.flatMap(
        (exchange) =>
          (exchange.responseBody as { items: { betId: string }[] }).items,
      );
      expect(items.map((item) => item.betId)).toContain(outcome.betId);
    },
    flowTimeoutMs,
  );
});
