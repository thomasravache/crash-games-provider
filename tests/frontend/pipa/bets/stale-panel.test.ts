import { BetError } from "@apogeu/game-sdk";
import {
  initialBetsState,
  reduceBetsEvent,
  type BetErrorCode,
  type BetsState,
} from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import { bettingEvent, flyingEvent } from "../support/rounds";
import {
  balanceRead,
  betAccepted,
  limitsRead,
  money,
  placeRequested,
  roundBetting,
  roundFlying,
} from "./support/bets";
import { startedRig, type BetsRig } from "./support/bets-rig";
import { hubError } from "./support/fake-bets-hub";

// SPEC-0026 UT-09 (amendment 2026-10-12, review G4): a panel placed or active from a previous round (its BetSettled was
// lost in a connection drop and GET /api/v1/me/bets is not there yet) goes back to idle when the betting phase of
// another round starts; a BET_NOT_ACTIVE or BET_NOT_FOUND answer to RETIRAR or CANCELAR also returns the panel to idle.

const staleCodes = [
  "BET_NOT_ACTIVE",
  "BET_NOT_FOUND",
] as const satisfies readonly BetErrorCode[];

/** Panel 1 placed (bet-1) and panel 2 placed (bet-2) on round-41; `fly` makes both active. */
function placedState(fly: boolean): BetsState {
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
  return fly ? reduceBetsEvent(state, roundFlying("round-41")) : state;
}

const t0 = Date.parse("2026-10-12T12:00:00.000Z");

/** Client with bet-1 on panel 1 of round 41, placed (or active when `fly`). */
async function rigWithBet(fly: boolean): Promise<BetsRig> {
  const rig = await startedRig("3000.00");
  rig.round(bettingEvent(41, t0 + 6_000));
  rig.hub.respond("PlaceBet", (request) =>
    Promise.resolve({
      betId: "bet-1",
      clientBetId: request.clientBetId,
      roundId: "round-41",
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
  if (fly) {
    rig.round(flyingEvent(41, t0 + 6_000));
    await rig.until((state) => state.panels[0].status === "active");
  }
  return rig;
}

describe("SPEC-0026 painel preso de rodada anterior", () => {
  test("SPEC-0026:UT-09 a fase de apostas de outra rodada devolve a idle os painéis placed e active da rodada anterior", () => {
    for (const fly of [false, true]) {
      const stale = placedState(fly);
      expect(stale.panels.map((panel) => panel.status)).toEqual(
        fly ? ["active", "active"] : ["placed", "placed"],
      );
      const next = reduceBetsEvent(stale, roundBetting("round-42"));
      for (const panel of next.panels) {
        expect(
          panel,
          `fly=${String(fly)} panel ${String(panel.panel)}`,
        ).toMatchObject({
          status: "idle",
          request: "none",
          betId: null,
          roundId: null,
        });
      }
      // The balance stays the last one the server confirmed.
      expect(next.balance).toBe(stale.balance);
    }
  });

  test("SPEC-0026:UT-09 a fase de apostas da própria rodada não toca o painel; uma aposta queued sobrevive à rodada seguinte", () => {
    const placed = placedState(false);
    expect(reduceBetsEvent(placed, roundBetting("round-41"))).toBe(placed);

    // Panel 1 flying on round-41, panel 2 queued for the next round during that flight.
    let queued = reduceBetsEvent(
      reduceBetsEvent(
        reduceBetsEvent(initialBetsState, limitsRead),
        balanceRead("3000.00"),
      ),
      roundBetting("round-41"),
    );
    queued = reduceBetsEvent(queued, placeRequested(1, "c-1", "10.00"));
    queued = reduceBetsEvent(
      queued,
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
    queued = reduceBetsEvent(queued, roundFlying("round-41"));
    queued = reduceBetsEvent(queued, placeRequested(2, "c-3", "5.00"));
    queued = reduceBetsEvent(
      queued,
      betAccepted({
        panel: 2,
        betId: "bet-3",
        clientBetId: "c-3",
        roundId: null,
        amount: "5.00",
        status: "queued",
        balance: "2985.00",
      }),
    );
    expect(queued.panels[1]).toMatchObject({
      status: "queued",
      betId: "bet-3",
    });
    const next = reduceBetsEvent(queued, roundBetting("round-42"));
    expect(next.panels[1]).toMatchObject({ status: "queued", betId: "bet-3" });
    expect(next.panels[0]).toMatchObject({ status: "idle", betId: null });
  });

  test("SPEC-0026:UT-09 requestFailed BET_NOT_ACTIVE ou BET_NOT_FOUND devolve a idle um painel placed, active ou cashing_out", () => {
    for (const code of staleCodes) {
      const placed = placedState(false);
      const active = placedState(true);
      const cashing = reduceBetsEvent(
        reduceBetsEvent(active, { type: "cashOutRequested", panel: 1 }),
        { type: "cashOutPending", betId: "bet-1" },
      );
      for (const [name, state] of [
        [
          "placed",
          reduceBetsEvent(placed, { type: "cancelRequested", panel: 1 }),
        ],
        [
          "active",
          reduceBetsEvent(active, { type: "cashOutRequested", panel: 1 }),
        ],
        ["cashing_out", cashing],
      ] as const) {
        const failed = reduceBetsEvent(state, {
          type: "requestFailed",
          panel: 1,
          code,
        });
        expect(failed.panels[0], `${code} on ${name}`).toMatchObject({
          status: "idle",
          request: "none",
          betId: null,
        });
        // The other panel keeps its own bet.
        expect(failed.panels[1], `${code} on ${name}`).toEqual(state.panels[1]);
      }
    }
  });

  test("SPEC-0026:UT-09 RETIRAR respondido com BET_NOT_ACTIVE ou BET_NOT_FOUND pelo hub devolve o painel a idle", async () => {
    for (const code of staleCodes) {
      const rig = await rigWithBet(true);
      rig.hub.respond("CashOut", () =>
        Promise.reject(hubError("CashOut", code)),
      );
      const error = await rig.client.cashOut(1).then(
        () => undefined,
        (reason: unknown) => reason,
      );
      expect(error, code).toBeInstanceOf(BetError);
      expect((error as BetError).code, code).toBe(code);
      expect(rig.state().panels[0], code).toMatchObject({
        status: "idle",
        request: "none",
        betId: null,
      });
      expect(rig.state().balance, code).toBe("2990.00");
      await rig.client.stop();
    }
  });

  test("SPEC-0026:UT-09 CANCELAR respondido com BET_NOT_ACTIVE ou BET_NOT_FOUND pelo hub devolve o painel a idle", async () => {
    for (const code of staleCodes) {
      const rig = await rigWithBet(false);
      rig.hub.respond("CancelBet", () =>
        Promise.reject(hubError("CancelBet", code)),
      );
      const error = await rig.client.cancelBet(1).then(
        () => undefined,
        (reason: unknown) => reason,
      );
      expect(error, code).toBeInstanceOf(BetError);
      expect(rig.state().panels[0], code).toMatchObject({
        status: "idle",
        request: "none",
        betId: null,
      });
      await rig.client.stop();
    }
  });
});
