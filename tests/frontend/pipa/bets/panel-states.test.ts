import {
  BetPanel,
  betPanelText,
  betPanelView,
  betTestIds,
  type BetPanelProps,
} from "@apogeu/game-ui";
import {
  createBetsStore,
  initialBetsState,
  reduceBetsEvent,
  type BetsEvent,
  type BetsState,
  type BetsStore,
  type PanelNumber,
  type PanelState,
  type RoundPhase,
} from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import {
  bettingEvent,
  crashedEvent,
  flyingEvent,
  roundIds,
} from "../support/rounds";
import {
  balanceRead,
  balanceUpdated,
  betAccepted,
  betActivated,
  betRefunded,
  betSettled,
  cashedOut,
  draft,
  limits,
  limitsRead,
  money,
  multiplier,
  placeRequested,
  roundAnnulled,
  roundBetting,
  roundCrashed,
  roundFlying,
} from "./support/bets";
import { startedRig } from "./support/bets-rig";
import { attributesOf, render, textOf } from "./support/react";

// SPEC-0026 UT-01: the sequence of server events (BetAccepted placed, RoundFlying, CashedOut; BetAccepted queued,
// BetActivated, RoundCrashed with BetSettled; CashOutPending followed by CashedOut and by CashOutRejected; CashedOut
// with creditStatus "pending" followed by BalanceUpdated; BetRefunded of an annulled round), applied to the bets store,
// moves the panels through the states of the contract and the balance changes only to values the server sent.
//
// The server balances below are deliberately NOT the arithmetic of the bets (for example 3000 − 10 = "2991.00"): the
// store must copy the server value, never compute its own.

const panelOf = (state: BetsState, panel: PanelNumber): PanelState =>
  state.panels[panel - 1] as PanelState;

/** Store with the limits and the balance read (what start() leaves). */
function readyStore(balance = "3000.00"): BetsStore {
  const store = createBetsStore();
  store.dispatch(limitsRead);
  store.dispatch(balanceRead(balance));
  return store;
}

/** Dispatches `events` in order, recording the balance after each. */
function apply(
  store: BetsStore,
  events: readonly BetsEvent[],
): (string | null)[] {
  return events.map((event) => {
    store.dispatch(event);
    return store.getState().balance;
  });
}

const view = (
  panel: PanelState,
  phase: RoundPhase,
  current = 1,
): ReturnType<typeof betPanelView> =>
  betPanelView({
    panel,
    draft: draft("10,00"),
    limits,
    phase,
    multiplier: current,
  });

describe("SPEC-0026 transições do painel de aposta", () => {
  test("SPEC-0026:UT-01 o store começa com os dois painéis idle, sem saldo nem limites (vazios até a primeira leitura)", () => {
    expect(initialBetsState.balance).toBeNull();
    expect(initialBetsState.limits).toBeNull();
    expect(
      initialBetsState.panels.map((panel) => [panel.panel, panel.status]),
    ).toEqual([
      [1, "idle"],
      [2, "idle"],
    ]);

    const store = createBetsStore();
    expect(store.getState()).toEqual(initialBetsState);
    store.dispatch(limitsRead);
    store.dispatch(balanceRead("3000.00"));
    expect(store.getState()).toMatchObject({
      balance: "3000.00",
      currency: "BRL",
      limits,
    });
  });

  test("SPEC-0026:UT-01 BetAccepted placed → RoundFlying → CashedOut: idle → placed → active → cashed_out e idle na rodada seguinte", () => {
    const store = readyStore();
    const notified: BetsState[] = [];
    store.subscribe(() => notified.push(store.getState()));

    store.dispatch(roundBetting("round-41"));
    store.dispatch(placeRequested(1, "c-1", "10.00"));
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "idle",
      request: "placing",
    });
    // No optimistic balance: the request alone changes nothing of the money.
    expect(store.getState().balance).toBe("3000.00");

    store.dispatch(
      betAccepted({
        panel: 1,
        betId: "bet-1",
        clientBetId: "c-1",
        roundId: "round-41",
        amount: "10.00",
        status: "placed",
        balance: "2991.00",
      }),
    );
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "placed",
      request: "none",
      betId: "bet-1",
      clientBetId: "c-1",
      roundId: "round-41",
      amount: "10.00",
      error: null,
    });
    expect(store.getState().balance).toBe("2991.00");
    expect(panelOf(store.getState(), 2).status).toBe("idle");

    store.dispatch(roundFlying("round-41"));
    expect(panelOf(store.getState(), 1).status).toBe("active");

    store.dispatch({ type: "cashOutRequested", panel: 1 });
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "active",
      request: "cashing_out",
    });

    store.dispatch(
      cashedOut({
        betId: "bet-1",
        multiplier: "2.47",
        payout: "24.70",
        balance: "3020.00",
      }),
    );
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "cashed_out",
      request: "none",
      multiplier: "2.47",
      payout: "24.70",
      auto: false,
      creditStatus: "credited",
    });
    expect(store.getState().balance).toBe("3020.00");

    // The win stays on the panel until the next round opens.
    store.dispatch(roundCrashed("round-41"));
    expect(panelOf(store.getState(), 1).status).toBe("cashed_out");
    store.dispatch(roundBetting("round-42"));
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "idle",
      betId: null,
      payout: null,
    });
    expect(store.getState().balance).toBe("3020.00");

    expect(notified.length).toBeGreaterThan(0);
  });

  test("SPEC-0026:UT-01 BetAccepted queued → BetActivated → RoundCrashed com BetSettled: queued → placed → active → lost", () => {
    const store = readyStore();
    store.dispatch(roundFlying("round-41"));

    store.dispatch(placeRequested(2, "c-2", "5.00"));
    store.dispatch(
      betAccepted({
        panel: 2,
        betId: "bet-2",
        clientBetId: "c-2",
        roundId: null,
        amount: "5.00",
        status: "queued",
        balance: "2996.00",
      }),
    );
    expect(panelOf(store.getState(), 2)).toMatchObject({
      status: "queued",
      roundId: null,
      betId: "bet-2",
    });
    expect(store.getState().balance).toBe("2996.00");

    // The flight of the current round does not touch a bet queued for the next one.
    store.dispatch(roundCrashed("round-41"));
    expect(panelOf(store.getState(), 2).status).toBe("queued");

    store.dispatch(roundBetting("round-42"));
    store.dispatch(betActivated("bet-2", "round-42"));
    expect(panelOf(store.getState(), 2)).toMatchObject({
      status: "placed",
      roundId: "round-42",
    });

    store.dispatch(roundFlying("round-42"));
    expect(panelOf(store.getState(), 2).status).toBe("active");

    const balances = apply(store, [
      roundCrashed("round-42"),
      betSettled("bet-2", "round-42"),
    ]);
    expect(panelOf(store.getState(), 2)).toMatchObject({
      status: "lost",
      betId: "bet-2",
    });
    // The stake was debited at the acceptance: the loss changes nothing of the balance.
    expect(balances).toEqual(["2996.00", "2996.00"]);

    store.dispatch(roundBetting("round-43"));
    expect(panelOf(store.getState(), 2).status).toBe("idle");
  });

  test("SPEC-0026:UT-01 CashOutPending → cashing_out, decidido por CashedOut (cashed_out) ou por CashOutRejected (lost)", () => {
    const store = readyStore();
    const activeBet = (panel: PanelNumber, betId: string): void => {
      store.dispatch(placeRequested(panel, `c-${betId}`, "10.00"));
      store.dispatch(
        betAccepted({
          panel,
          betId,
          clientBetId: `c-${betId}`,
          roundId: "round-41",
          amount: "10.00",
          status: "placed",
          balance: store.getState().balance ?? "0.00",
        }),
      );
    };
    store.dispatch(roundBetting("round-41"));
    activeBet(1, "bet-1");
    activeBet(2, "bet-2");
    store.dispatch(roundFlying("round-41"));
    const before = store.getState().balance;

    for (const panel of [1, 2] as const) {
      store.dispatch({ type: "cashOutRequested", panel });
    }
    store.dispatch({ type: "cashOutPending", betId: "bet-1" });
    store.dispatch({ type: "cashOutPending", betId: "bet-2" });
    expect(panelOf(store.getState(), 1).status).toBe("cashing_out");
    expect(panelOf(store.getState(), 2).status).toBe("cashing_out");
    expect(store.getState().balance).toBe(before);

    store.dispatch(
      cashedOut({
        betId: "bet-1",
        multiplier: "1.80",
        payout: "18.00",
        balance: "3007.00",
      }),
    );
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "cashed_out",
      payout: "18.00",
    });
    expect(store.getState().balance).toBe("3007.00");

    store.dispatch({
      type: "cashOutRejected",
      betId: "bet-2",
      code: "CASHOUT_TOO_LATE",
    });
    expect(panelOf(store.getState(), 2)).toMatchObject({
      status: "lost",
      payout: null,
    });
    expect(store.getState().balance).toBe("3007.00");
  });

  test("SPEC-0026:UT-01 CashedOut com creditStatus pending mostra o ganho; o saldo só muda com BalanceUpdated", () => {
    const store = readyStore();
    store.dispatch(roundBetting("round-41"));
    store.dispatch(placeRequested(1, "c-1", "10.00"));
    store.dispatch(
      betAccepted({
        panel: 1,
        betId: "bet-1",
        clientBetId: "c-1",
        roundId: "round-41",
        amount: "10.00",
        status: "placed",
        balance: "2990.00",
      }),
    );
    store.dispatch(roundFlying("round-41"));

    store.dispatch(
      cashedOut({
        betId: "bet-1",
        multiplier: "3.00",
        payout: "30.00",
        creditStatus: "pending",
        balance: null,
      }),
    );
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "cashed_out",
      payout: "30.00",
      creditStatus: "pending",
    });
    expect(store.getState().balance).toBe("2990.00");

    store.dispatch(balanceUpdated("3021.00"));
    expect(store.getState().balance).toBe("3021.00");
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "cashed_out",
      creditStatus: "credited",
    });
  });

  test("SPEC-0026:UT-01 BetRefunded de rodada anulada: placed e active → refunded, com o saldo do servidor (ou pendente até BalanceUpdated)", () => {
    const store = readyStore();
    store.dispatch(roundBetting("round-41"));
    for (const panel of [1, 2] as const) {
      store.dispatch(placeRequested(panel, `c-${String(panel)}`, "10.00"));
      store.dispatch(
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
    store.dispatch(roundFlying("round-41"));
    store.dispatch(roundAnnulled("round-41"));

    store.dispatch(
      betRefunded({
        betId: "bet-1",
        roundId: "round-41",
        refunded: "10.00",
        balance: "2992.00",
      }),
    );
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "refunded",
      refunded: "10.00",
    });
    expect(store.getState().balance).toBe("2992.00");

    store.dispatch(
      betRefunded({
        betId: "bet-2",
        roundId: "round-41",
        refunded: "10.00",
        balance: null,
      }),
    );
    expect(panelOf(store.getState(), 2)).toMatchObject({
      status: "refunded",
      refunded: "10.00",
      creditStatus: "pending",
    });
    expect(store.getState().balance).toBe("2992.00");
    store.dispatch(balanceUpdated("3000.00"));
    expect(store.getState().balance).toBe("3000.00");
  });

  test("SPEC-0026:UT-01 BetCancelled volta o painel a idle com o saldo do servidor", () => {
    const store = readyStore();
    store.dispatch(roundBetting("round-41"));
    store.dispatch(placeRequested(1, "c-1", "10.00"));
    store.dispatch(
      betAccepted({
        panel: 1,
        betId: "bet-1",
        clientBetId: "c-1",
        roundId: "round-41",
        amount: "10.00",
        status: "placed",
        balance: "2990.00",
      }),
    );
    store.dispatch({ type: "cancelRequested", panel: 1 });
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "placed",
      request: "cancelling",
    });

    store.dispatch({
      type: "betCancelled",
      betId: "bet-1",
      refunded: money("10.00"),
      refundStatus: "refunded",
      balance: money("3000.00"),
    });
    expect(panelOf(store.getState(), 1)).toMatchObject({
      status: "idle",
      betId: null,
      request: "none",
    });
    expect(store.getState().balance).toBe("3000.00");
  });

  test("SPEC-0026:UT-01 eventos de aposta desconhecida ou repetidos não mudam o estado", () => {
    const state = reduceBetsEvent(
      reduceBetsEvent(initialBetsState, limitsRead),
      balanceRead("3000.00"),
    );
    for (const event of [
      cashedOut({
        betId: "unknown",
        multiplier: "2.00",
        payout: "20.00",
        balance: "9999.00",
      }),
      betSettled("unknown", "round-41"),
      betActivated("unknown", "round-41"),
      { type: "cashOutRejected", betId: "unknown", code: "CASHOUT_TOO_LATE" },
    ] satisfies BetsEvent[]) {
      expect(reduceBetsEvent(state, event), event.type).toBe(state);
    }

    const accepted = betAccepted({
      panel: 1,
      betId: "bet-1",
      clientBetId: "c-1",
      roundId: "round-41",
      amount: "10.00",
      status: "placed",
      balance: "2990.00",
    });
    const once = reduceBetsEvent(
      reduceBetsEvent(state, placeRequested(1, "c-1", "10.00")),
      accepted,
    );
    const flying = reduceBetsEvent(once, roundFlying("round-41"));
    const won = reduceBetsEvent(
      flying,
      cashedOut({
        betId: "bet-1",
        multiplier: "2.00",
        payout: "20.00",
        balance: "3010.00",
      }),
    );
    // The same CashedOut again (result and server message of one cash-out) is ignored.
    expect(
      reduceBetsEvent(
        won,
        cashedOut({
          betId: "bet-1",
          multiplier: "2.00",
          payout: "20.00",
          balance: "3010.00",
        }),
      ),
    ).toBe(won);
  });

  test("SPEC-0026:UT-01 o painel mostra cada estado: APOSTAR, CANCELAR, PRÓXIMA, RETIRAR R$ valor×m, Retirando…, ganho, perda, estorno", () => {
    const idle = initialBetsState.panels[0];
    const base: PanelState = {
      ...idle,
      betId: "bet-1",
      clientBetId: "c-1",
      roundId: "round-41",
      amount: money("10.00"),
    };

    expect(view(idle, "betting")).toMatchObject({
      title: "APOSTA 1",
      action: "bet",
      label: "APOSTAR R$ 10,00",
      badge: null,
      disabled: false,
      editable: true,
      result: null,
      error: null,
    });
    expect(view({ ...base, status: "placed" }, "betting")).toMatchObject({
      action: "cancel",
      label: "CANCELAR",
      badge: null,
      editable: false,
    });
    expect(
      view({ ...base, roundId: null, status: "queued" }, "flying", 3.1),
    ).toMatchObject({
      action: "cancel",
      label: "CANCELAR",
      badge: "PRÓXIMA",
    });
    expect(view({ ...base, status: "active" }, "flying", 2.47)).toMatchObject({
      action: "cashOut",
      label: "RETIRAR R$ 24,70",
      disabled: false,
    });
    expect(
      view({ ...base, status: "cashing_out" }, "flying", 2.6),
    ).toMatchObject({
      action: "none",
      label: "Retirando…",
      disabled: true,
    });
    expect(
      view(
        {
          ...base,
          status: "cashed_out",
          multiplier: multiplier("2.47"),
          payout: money("24.70"),
          creditStatus: "credited",
        },
        "crashed",
        3,
      ),
    ).toMatchObject({ result: "Ganhou R$ 24,70", note: null });
    expect(
      view(
        {
          ...base,
          status: "cashed_out",
          multiplier: multiplier("2.47"),
          payout: money("24.70"),
          creditStatus: "pending",
        },
        "crashed",
        3,
      ),
    ).toMatchObject({
      result: "Ganhou R$ 24,70",
      note: "Crédito em processamento",
    });
    expect(view({ ...base, status: "lost" }, "crashed", 1.2)).toMatchObject({
      result: "Perdeu",
    });
    expect(
      view(
        { ...base, status: "refunded", refunded: money("10.00") },
        "annulled",
      ),
    ).toMatchObject({ result: "Estornado R$ 10,00" });
    expect(
      betPanelView({
        panel: { ...idle, panel: 2 },
        draft: draft("10,00"),
        limits,
        phase: "idle",
        multiplier: 1,
      }).title,
    ).toBe("APOSTA 2");
  });

  test("SPEC-0026:UT-01 BetPanel renderiza a view com os test ids e rótulos do contrato", () => {
    const panel: PanelState = {
      ...initialBetsState.panels[0],
      status: "active",
      betId: "bet-1",
      roundId: "round-41",
      amount: money("10.00"),
    };
    const props: BetPanelProps = {
      panel,
      draft: draft("10,00"),
      limits,
      phase: "flying",
      multiplier: 2.47,
      onDraftChange: () => undefined,
      onPlaceBet: () => undefined,
      onCancel: () => undefined,
      onCashOut: () => undefined,
    };
    const markup = render(BetPanel, props);

    expect(attributesOf(markup, betTestIds.panel(1))).not.toBeNull();
    expect(textOf(markup, betTestIds.action(1))).toBe(
      betPanelText.cashOut("R$ 24,70"),
    );
    expect(attributesOf(markup, betTestIds.amount(1))).not.toBeNull();
    expect(markup).toContain(betPanelText.title(1));
    expect(markup).toContain(betPanelText.autoCashOut);
  });

  test("SPEC-0026:UT-01 o cliente traduz BetAccepted, fases da rodada, BetActivated, CashedOut, BetSettled, BetRefunded e BalanceUpdated no store", async () => {
    const rig = await startedRig("3000.00");
    for (const event of [
      "BetActivated",
      "CashedOut",
      "CashOutRejected",
      "BalanceUpdated",
      "BetSettled",
      "BetRefunded",
    ] as const) {
      expect(rig.hub.listens(event), event).toBe(true);
    }

    const t0 = Date.parse("2026-10-11T12:00:00.000Z");
    rig.round(bettingEvent(41, t0 + 6_000));
    rig.hub.respond("PlaceBet", (request) =>
      Promise.resolve({
        betId: "bet-1",
        clientBetId: request.clientBetId,
        roundId: roundIds(41).roundId,
        roundNumber: 41,
        panel: request.panel,
        amount: request.amount,
        currency: "BRL",
        autoCashOut: request.autoCashOut,
        status: "placed",
        balance: money("2990.00"),
      }),
    );
    await rig.client.placeBet(1, { amount: money("10.00"), autoCashOut: null });
    expect(panelOf(rig.state(), 1)).toMatchObject({
      status: "placed",
      betId: "bet-1",
    });
    expect(rig.state().balance).toBe("2990.00");

    rig.round(flyingEvent(41, t0 + 6_000));
    await rig.until((state) => panelOf(state, 1).status === "active");

    rig.hub.emit("CashedOut", {
      betId: "bet-1",
      multiplier: multiplier("1.50"),
      payout: money("15.00"),
      creditStatus: "pending",
      balance: null,
      auto: true,
    });
    await rig.until((state) => panelOf(state, 1).status === "cashed_out");
    expect(panelOf(rig.state(), 1)).toMatchObject({
      auto: true,
      payout: "15.00",
      creditStatus: "pending",
    });
    rig.hub.emit("BalanceUpdated", {
      balance: money("3005.00"),
      currency: "BRL",
    });
    await rig.until((state) => state.balance === "3005.00");

    // Next round: a queued bet of panel 2 is activated and then lost.
    rig.round(crashedEvent(41, "1.62", t0 + 20_000));
    rig.hub.respond("PlaceBet", (request) =>
      Promise.resolve({
        betId: "bet-2",
        clientBetId: request.clientBetId,
        roundId: null,
        roundNumber: null,
        panel: request.panel,
        amount: request.amount,
        currency: "BRL",
        autoCashOut: request.autoCashOut,
        status: "queued",
        balance: money("2995.00"),
      }),
    );
    await rig.client.placeBet(2, { amount: money("10.00"), autoCashOut: null });
    expect(panelOf(rig.state(), 2).status).toBe("queued");
    rig.round(bettingEvent(42, t0 + 30_000));
    rig.hub.emit("BetActivated", {
      betId: "bet-2",
      roundId: roundIds(42).roundId,
      roundNumber: 42,
    });
    await rig.until((state) => panelOf(state, 2).status === "placed");
    expect(panelOf(rig.state(), 1).status).toBe("idle");
    rig.round(flyingEvent(42, t0 + 30_000));
    await rig.until((state) => panelOf(state, 2).status === "active");
    rig.round(crashedEvent(42, "1.10", t0 + 32_000));
    rig.hub.emit("BetSettled", {
      betId: "bet-2",
      roundId: roundIds(42).roundId,
      status: "lost",
    });
    await rig.until((state) => panelOf(state, 2).status === "lost");
    expect(rig.state().balance).toBe("2995.00");

    // An annulled round refunds the bet with the server balance.
    rig.round(bettingEvent(43, t0 + 40_000));
    rig.hub.respond("PlaceBet", (request) =>
      Promise.resolve({
        betId: "bet-3",
        clientBetId: request.clientBetId,
        roundId: roundIds(43).roundId,
        roundNumber: 43,
        panel: request.panel,
        amount: request.amount,
        currency: "BRL",
        autoCashOut: request.autoCashOut,
        status: "placed",
        balance: money("2985.00"),
      }),
    );
    await rig.client.placeBet(1, { amount: money("10.00"), autoCashOut: null });
    rig.round({
      type: "annulled",
      roundId: roundIds(43).roundId,
      reason: "test",
    });
    rig.hub.emit("BetRefunded", {
      betId: "bet-3",
      roundId: roundIds(43).roundId,
      refunded: money("10.00"),
      refundStatus: "refunded",
      balance: money("2995.00"),
    });
    await rig.until((state) => panelOf(state, 1).status === "refunded");
    expect(rig.state().balance).toBe("2995.00");
    await rig.client.stop();
  });
});
