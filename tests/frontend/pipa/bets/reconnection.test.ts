import { fetchMyBets, fetchWalletBalance } from "@apogeu/game-sdk";
import {
  initialBetsState,
  reduceBetsEvent,
  type BetsState,
  type OpenBet,
  type PanelNumber,
  type PanelState,
} from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import { bettingEvent } from "../support/rounds";
import {
  apiBaseUrl,
  balanceRead,
  betAccepted,
  limitsRead,
  money,
  placeRequested,
  roundBetting,
  sessionToken,
} from "./support/bets";
import { startedRig, type BetsRig } from "./support/bets-rig";
import { FakeWallet, myBet } from "./support/fake-wallet";

// SPEC-0026 UT-03: with stale data in the store, when the connection comes back the balance is replaced by the one
// read from the wallet (SPEC-0021@4) and the bets by those of GET /api/v1/me/bets (SPEC-0025@1); while that endpoint
// answers 404, the local panels are kept, without error, and only the balance is read again.

const panelOf = (state: BetsState, panel: PanelNumber): PanelState =>
  state.panels[panel - 1] as PanelState;

/** Store with stale data: balance 2990,00 and both panels placed on round-41. */
function staleState(): BetsState {
  let state = reduceBetsEvent(
    reduceBetsEvent(initialBetsState, limitsRead),
    balanceRead("3000.00"),
  );
  state = reduceBetsEvent(state, roundBetting("round-41"));
  for (const panel of [1, 2] as const) {
    state = reduceBetsEvent(
      state,
      placeRequested(panel, `c-${String(panel)}`, "10.00"),
    );
    state = reduceBetsEvent(
      state,
      betAccepted({
        panel,
        betId: `bet-${String(panel)}`,
        clientBetId: `c-${String(panel)}`,
        roundId: "round-41",
        amount: "10.00",
        status: "placed",
        balance: panel === 1 ? "2990.00" : "2980.00",
      }),
    );
  }
  return state;
}

/** Client started, with bet-1 placed on panel 1 and bet-2 placed on panel 2 (round 41 in betting). */
async function rigWithTwoBets(): Promise<BetsRig> {
  const rig = await startedRig("3000.00");
  rig.round(bettingEvent(41, Date.parse("2026-10-11T12:00:06.000Z")));
  let next = 0;
  rig.hub.respond("PlaceBet", (request) => {
    next += 1;
    return Promise.resolve({
      betId: `bet-${String(next)}`,
      clientBetId: request.clientBetId,
      roundId: "round-41",
      roundNumber: 41,
      panel: request.panel,
      amount: request.amount,
      currency: "BRL",
      autoCashOut: request.autoCashOut,
      status: "placed",
      balance: money(next === 1 ? "2990.00" : "2980.00"),
    });
  });
  await rig.client.placeBet(1, { amount: money("10.00"), autoCashOut: null });
  await rig.client.placeBet(2, { amount: money("10.00"), autoCashOut: null });
  return rig;
}

describe("SPEC-0026 reconexão do painel de aposta", () => {
  test("SPEC-0026:UT-03 reloaded substitui o saldo e os painéis pelas apostas abertas do servidor", () => {
    const stale = staleState();
    const openBets: OpenBet[] = [
      {
        betId: "bet-1",
        roundId: "round-41",
        panel: 1,
        amount: money("10.00"),
        autoCashOut: null,
        status: "active",
      },
    ];
    const reloaded = reduceBetsEvent(stale, {
      type: "reloaded",
      balance: money("2500.00"),
      currency: "BRL",
      openBets,
    });
    expect(reloaded.balance).toBe("2500.00");
    expect(panelOf(reloaded, 1)).toMatchObject({
      status: "active",
      betId: "bet-1",
      amount: "10.00",
    });
    // Panel 2 has no open bet on the server any more.
    expect(panelOf(reloaded, 2)).toMatchObject({ status: "idle", betId: null });
    expect(reloaded.limits).toEqual(stale.limits);
  });

  test("SPEC-0026:UT-03 reloaded sem o histórico (404 → null) mantém os painéis locais e relê só o saldo", () => {
    const stale = staleState();
    const reloaded = reduceBetsEvent(stale, {
      type: "reloaded",
      balance: money("2500.00"),
      currency: "BRL",
      openBets: null,
    });
    expect(reloaded.balance).toBe("2500.00");
    expect(reloaded.panels).toEqual(stale.panels);
  });

  test("SPEC-0026:UT-03 na reconexão o cliente relê o saldo e GET /api/v1/me/bets com a sessão e substitui o store", async () => {
    const rig = await rigWithTwoBets();
    expect(rig.state().balance).toBe("2980.00");
    const balanceReads = rig.wallet.requestsTo("/api/v1/wallet/balance").length;

    // While offline the round flew, panel 2 was settled and the balance moved on the server.
    rig.wallet.balance = "2500.00";
    rig.wallet.myBets = [
      myBet({
        betId: "bet-1",
        panel: 1,
        roundId: "round-41",
        amount: "10.00",
        status: "active",
      }),
      myBet({
        betId: "bet-2",
        panel: 2,
        roundId: "round-41",
        amount: "10.00",
        status: "lost",
      }),
      myBet({
        betId: "bet-0",
        panel: 1,
        roundId: "round-40",
        amount: "5.00",
        status: "cashed_out",
      }),
    ];
    rig.hub.dropConnection();
    rig.hub.restoreConnection();

    await rig.until(
      (state) =>
        state.balance === "2500.00" && panelOf(state, 2).status === "idle",
    );
    expect(panelOf(rig.state(), 1)).toMatchObject({
      status: "active",
      betId: "bet-1",
    });
    expect(rig.wallet.requestsTo("/api/v1/wallet/balance").length).toBe(
      balanceReads + 1,
    );
    const history = rig.wallet.requestsTo("/api/v1/me/bets");
    expect(history.length).toBeGreaterThan(0);
    for (const request of [
      ...history,
      ...rig.wallet.requestsTo("/api/v1/wallet/balance"),
    ]) {
      expect(request.authorization).toBe(`Bearer ${sessionToken}`);
    }
    await rig.client.stop();
  });

  test("SPEC-0026:UT-03 com GET /api/v1/me/bets respondendo 404 os painéis locais são mantidos sem erro", async () => {
    const rig = await rigWithTwoBets();
    const panels = rig.state().panels;
    rig.wallet.balance = "2500.00";
    rig.wallet.myBets = null;

    rig.hub.dropConnection();
    rig.hub.restoreConnection();
    await rig.until((state) => state.balance === "2500.00");

    expect(rig.state().panels).toEqual(panels);
    expect(rig.state().panels.map((panel) => panel.error)).toEqual([
      null,
      null,
    ]);
    await expect(rig.client.reload()).resolves.toBeUndefined();
    await rig.client.stop();
  });

  test("SPEC-0026:UT-03 API da carteira: saldo com Bearer e GET /api/v1/me/bets resolve null no 404", async () => {
    const wallet = new FakeWallet("1234.50");
    await expect(
      fetchWalletBalance(apiBaseUrl, sessionToken, wallet.fetch),
    ).resolves.toEqual({
      balance: "1234.50",
      currency: "BRL",
      mode: "demo",
    });
    await expect(
      fetchMyBets(apiBaseUrl, sessionToken, wallet.fetch, 20),
    ).resolves.toBeNull();
    wallet.myBets = [
      myBet({
        betId: "bet-1",
        panel: 1,
        roundId: null,
        amount: "10.00",
        status: "queued",
      }),
    ];
    await expect(
      fetchMyBets(apiBaseUrl, sessionToken, wallet.fetch, 20),
    ).resolves.toMatchObject({
      items: [{ betId: "bet-1", status: "queued" }],
    });
    expect(
      wallet.requests.every(
        (request) => request.authorization === `Bearer ${sessionToken}`,
      ),
    ).toBe(true);
    expect(wallet.requests[0]?.url).toBe(`${apiBaseUrl}/api/v1/wallet/balance`);
  });
});
