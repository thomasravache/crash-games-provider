import {
  BetError,
  createBetsClient,
  createGameClient,
  createSignalRBetsConnection,
  createSignalRConnection,
  fetchWalletBalance,
  rechargeDemoWallet,
  WalletError,
  type BetsClient,
  type GameClient,
  type WalletBalance,
} from "@apogeu/game-sdk";
import type {
  BetsState,
  Money,
  PanelState,
  RoundState,
  RoundStateSource,
} from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import { exportHubManifest, openApiSchema } from "../support/contracts";
import { apiBaseUrl, launchDemo, waitUntil } from "../support/environment";
import { schemaErrors } from "../support/json-schema";
import { HttpRecorder } from "../support/recording";
import {
  betsHubSchema,
  BetsHubRecorder,
  cents,
} from "./support/bets-recording";

// SPEC-0026 IT-01, CT-01: the real game-sdk in Node (no browser) against the Host started by Aspire. One live flow is
// recorded once and shared: a demo player bets R$ 10,00 on panel 1, cashes out during the flight and reads the balance
// back. IT-01 asserts the store and the wallet (3.000 − 10 + payout, minus R$ 10 for each round lost before the cash
// out could land: crash points are random and a round may crash at 1,00x); CT-01 validates every BetsHub request,
// result and message against the hub contract (SPEC-0020@2 to SPEC-0024@2) and the wallet REST against the OpenAPI
// document (SPEC-0021@4).
//
// Run: APOGEU_API_URL=<base URL of the api resource> pnpm -C src/frontend test:integration
// (Aspire up with `dotnet run --project src/backend/AppHost`; CT-01 also needs `dotnet build Apogeu.slnx`).

/** Betting 6 s + flight up to the cap (≈ 154 s) + pause 3 s, with margin (SPEC-0008@2). */
const roundTimeoutMs = 190_000;
/** Rounds tried until a cash out lands (a crash at 1,00x or a late cash out loses the bet). */
const maxAttempts = 5;
const flowTimeoutMs = (maxAttempts + 1) * roundTimeoutMs;
const stake = "10.00";
const openingBalance = "3000.00";

interface LiveBet {
  readonly api: string;
  readonly sessionToken: string;
  readonly http: HttpRecorder;
  readonly hub: BetsHubRecorder;
  readonly lostRounds: number;
  /** Panel 1 when the cash out landed. */
  readonly won: PanelState;
  readonly storeAfterCredit: BetsState;
  readonly walletAfter: WalletBalance;
  readonly storeAfterReload: BetsState;
}

let liveBet: Promise<LiveBet> | undefined;

function followLiveBet(): Promise<LiveBet> {
  liveBet ??= runLiveBet();
  return liveBet;
}

const panel1 = (state: BetsState): PanelState => state.panels[0];

async function runLiveBet(): Promise<LiveBet> {
  const api = apiBaseUrl();
  const launch = await launchDemo(api);
  if (launch.status !== 302 || launch.launchToken === "") {
    throw new Error(
      `GET ${api}/dev/launch-demo must answer 302 with a launch token; got ${String(launch.status)}`,
    );
  }

  const http = new HttpRecorder();
  const hub = new BetsHubRecorder();
  // The bets client follows the round store of the game client, which starts it with the session token.
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
  const betsStore = bets.store;
  const onBets = (listener: () => void): (() => void) =>
    betsStore.subscribe(listener);
  const gameStore = game.store;
  const onRounds = (listener: () => void): (() => void) =>
    gameStore.subscribe(listener);

  try {
    await game.start(launch.launchToken);
    await waitUntil(
      onBets,
      () => {
        const state = betsStore.getState();
        return state.balance !== null && state.limits !== null
          ? true
          : undefined;
      },
      30_000,
      "the first wallet read and GetBetLimits",
    );
    expect(betsStore.getState().balance).toBe(openingBalance);

    let lostRounds = 0;
    for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
      await waitUntil(
        onRounds,
        () => (gameStore.getState().phase === "betting" ? true : undefined),
        roundTimeoutMs,
        "a round in betting",
      );
      await bets.placeBet(1, { amount: stake as Money, autoCashOut: null });
      const flying = await waitUntil(
        onBets,
        () => {
          const status = panel1(betsStore.getState()).status;
          return status === "active" ||
            status === "lost" ||
            status === "refunded"
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
          const status = panel1(betsStore.getState()).status;
          return status === "cashed_out" ||
            status === "lost" ||
            status === "refunded"
            ? status
            : undefined;
        },
        roundTimeoutMs,
        "the outcome of panel 1",
      );
      if (outcome === "cashed_out") {
        const won = panel1(betsStore.getState());
        const storeAfterCredit = await waitUntil(
          onBets,
          () => {
            const state = betsStore.getState();
            return panel1(state).creditStatus === "credited"
              ? state
              : undefined;
          },
          30_000,
          "the credit of the cash out (BalanceUpdated when pending)",
        );
        const sessionToken = sessionTokenOf(http, api);
        const walletAfter = await fetchWalletBalance(
          api,
          sessionToken,
          http.fetch,
        );
        await bets.reload();
        return {
          api,
          sessionToken,
          http,
          hub,
          lostRounds,
          won,
          storeAfterCredit,
          walletAfter,
          storeAfterReload: betsStore.getState(),
        };
      }
      if (outcome === "lost") lostRounds += 1;
      // Back to idle on the next betting phase before trying again.
      await waitUntil(
        onBets,
        () =>
          panel1(betsStore.getState()).status === "idle" ? true : undefined,
        roundTimeoutMs,
        "panel 1 back to idle",
      );
    }
    throw new Error(`no cash out landed in ${String(maxAttempts)} rounds`);
  } finally {
    await game.stop();
  }
}

function sessionTokenOf(http: HttpRecorder, api: string): string {
  const exchange = http.exchanges.find(
    (candidate) =>
      candidate.method === "POST" && candidate.url === `${api}/api/v1/sessions`,
  );
  const token = (
    exchange?.responseBody as { sessionToken?: unknown } | undefined
  )?.sessionToken;
  if (typeof token !== "string") throw new Error("no session token recorded");
  return token;
}

describe("SPEC-0026 SDK de apostas contra o backend real (Aspire)", () => {
  test(
    "SPEC-0026:IT-01 o game-sdk em Node aposta 10,00, retira no voo e relê o saldo: 3.000 − 10 + pagamento",
    async () => {
      const {
        hub,
        lostRounds,
        won,
        storeAfterCredit,
        walletAfter,
        storeAfterReload,
      } = await followLiveBet();

      const accepted = hub
        .named("PlaceBet", "result")
        .map((entry) => entry.payload as Record<string, unknown>);
      expect(accepted).toHaveLength(lostRounds + 1);
      for (const bet of accepted) {
        expect(bet).toMatchObject({ panel: 1, amount: stake, currency: "BRL" });
        expect(["placed", "queued"]).toContain(bet["status"]);
      }
      const lastBetId = accepted.at(-1)?.["betId"];
      expect(won.betId).toBe(lastBetId);

      // CashedOut came as the CashOut result or as the server message (decided pending, SPEC-0023@2).
      const cashOuts = [
        ...hub
          .named("CashOut", "result")
          .map((entry) => (entry.payload as { cashedOut: unknown }).cashedOut),
        ...hub.named("CashedOut", "message").map((entry) => entry.payload),
      ].filter(
        (entry): entry is Record<string, unknown> =>
          typeof entry === "object" &&
          entry !== null &&
          (entry as { betId?: unknown }).betId === lastBetId,
      );
      expect(cashOuts.length).toBeGreaterThan(0);
      const payout = cashOuts[0]?.["payout"];
      expect(won.payout).toBe(payout);
      expect(won.multiplier).toBe(cashOuts[0]?.["multiplier"]);
      expect(cents(payout)).toBeGreaterThanOrEqual(cents(stake));

      const expected =
        cents(openingBalance) -
        cents(stake) * BigInt(lostRounds + 1) +
        cents(payout);
      expect(cents(walletAfter.balance)).toBe(expected);
      expect(walletAfter.mode).toBe("demo");
      expect(storeAfterCredit.balance).toBe(walletAfter.balance);
      expect(storeAfterReload.balance).toBe(walletAfter.balance);
    },
    flowTimeoutMs,
  );

  test(
    "SPEC-0026:CT-01 mensagens reais do BetsHub e da carteira conferem com os contratos gerados (SPEC-0020@2 a SPEC-0024@2, SPEC-0021@4)",
    async () => {
      const [{ api, sessionToken, hub, http }, manifest] = await Promise.all([
        followLiveBet(),
        exportHubManifest(),
      ]);

      const names = new Set(
        hub.records.map((entry) => `${entry.name}:${entry.kind}`),
      );
      for (const name of [
        "GetBetLimits:result",
        "PlaceBet:request",
        "PlaceBet:result",
        "CashOut:request",
        "CashOut:result",
      ]) {
        expect(names, `the live flow recorded no ${name}`).toContain(name);
      }
      const violations = hub.records.flatMap((entry) =>
        schemaErrors(betsHubSchema(manifest, entry), entry.payload).map(
          (error) => `${entry.name} (${entry.kind}): ${error}`,
        ),
      );
      expect(violations).toEqual([]);

      // Money and multipliers travel as decimal strings, both ways.
      for (const entry of hub.records) {
        for (const field of [
          "amount",
          "balance",
          "payout",
          "refunded",
          "multiplier",
          "autoCashOut",
          "minBet",
          "maxBet",
          "maxWin",
        ]) {
          const value = (entry.payload as Record<string, unknown> | null)?.[
            field
          ];
          if (value !== undefined && value !== null) {
            expect(typeof value, `${entry.name} (${entry.kind}).${field}`).toBe(
              "string",
            );
          }
        }
      }

      const balances = http.exchanges.filter(
        (exchange) =>
          exchange.method === "GET" &&
          exchange.url === `${api}/api/v1/wallet/balance`,
      );
      expect(balances.length).toBeGreaterThanOrEqual(2);
      for (const exchange of balances) {
        expect(exchange.status).toBe(200);
        expect(
          schemaErrors(
            openApiSchema(
              "/api/v1/wallet/balance",
              "get",
              200,
              "application/json",
            ),
            exchange.responseBody,
          ),
        ).toEqual([]);
      }

      // With a balance far above the minimum bet the demo recharge is refused as ProblemDetails (SPEC-0021@4).
      const recharge = new HttpRecorder();
      const refused = await rechargeDemoWallet(
        api,
        sessionToken,
        recharge.fetch,
      ).then(
        () => undefined,
        (reason: unknown) => reason,
      );
      expect(refused).toBeInstanceOf(WalletError);
      expect((refused as WalletError).code).toBe("RECHARGE_NOT_ALLOWED");
      const exchange = recharge.exchanges[0];
      expect(exchange?.status).toBe(409);
      expect(exchange?.contentType).toMatch(/^application\/problem\+json/u);
      expect(
        schemaErrors(
          openApiSchema(
            "/api/v1/wallet/demo/recharge",
            "post",
            409,
            "application/problem+json",
          ),
          exchange?.responseBody,
        ),
      ).toEqual([]);
    },
    flowTimeoutMs,
  );
});
