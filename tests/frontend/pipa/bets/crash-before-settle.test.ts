import { betPanelView } from "@apogeu/game-ui";
import {
  initialBetsState,
  reduceBetsEvent,
  type BetsState,
  type PanelState,
} from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import {
  balanceRead,
  betAccepted,
  betSettled,
  draft,
  limits,
  limitsRead,
  money,
  placeRequested,
  roundAnnulled,
  roundBetting,
  roundCrashed,
  roundFlying,
} from "./support/bets";

// SPEC-0026 UT-08 (amendment 2026-10-12, review G4): an active panel whose round already crashed (or was annulled)
// while BetSettled has not arrived yet offers no RETIRAR: the action is disabled and shows no cash-out amount, and the
// store state does not change on the crash; a BetSettled arriving while the panel is cashing_out makes it lost.

/** Store state with panel 1 active on round-41 (bet-1, R$ 10,00). */
function activeState(): BetsState {
  let state = reduceBetsEvent(
    reduceBetsEvent(initialBetsState, limitsRead),
    balanceRead("3000.00"),
  );
  state = reduceBetsEvent(state, roundBetting("round-41"));
  state = reduceBetsEvent(state, placeRequested(1, "c-1", "10.00"));
  state = reduceBetsEvent(
    state,
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
  return reduceBetsEvent(state, roundFlying("round-41"));
}

describe("SPEC-0026 painel após o crash, antes do BetSettled", () => {
  test("SPEC-0026:UT-08 painel active com a rodada em crashed ou annulled: sem RETIRAR, ação desabilitada e sem valor de retirada", () => {
    const active = activeState();
    const panel = active.panels[0];
    expect(panel.status).toBe("active");

    // In flight RETIRAR is offered with the preview.
    expect(
      betPanelView({
        panel,
        draft: draft("10,00"),
        limits,
        phase: "flying",
        multiplier: 2.47,
      }),
    ).toMatchObject({
      action: "cashOut",
      label: "RETIRAR R$ 24,70",
      disabled: false,
    });

    for (const phase of ["crashed", "annulled"] as const) {
      const shown = betPanelView({
        panel,
        draft: draft("10,00"),
        limits,
        phase,
        multiplier: 2.47,
      });
      expect(shown.action, phase).not.toBe("cashOut");
      expect(shown.disabled, phase).toBe(true);
      expect(shown.label, phase).not.toMatch(/RETIRAR/u);
      expect(shown.label, phase).not.toMatch(/R\$/u);
    }

    // The crash itself changes nothing in the store: the outcome comes from the server.
    expect(reduceBetsEvent(active, roundCrashed("round-41"))).toBe(active);
    expect(reduceBetsEvent(active, roundAnnulled("round-41"))).toBe(active);
  });

  test("SPEC-0026:UT-08 BetSettled com o painel em cashing_out leva o painel a lost", () => {
    let state = reduceBetsEvent(activeState(), {
      type: "cashOutRequested",
      panel: 1,
    });
    state = reduceBetsEvent(state, { type: "cashOutPending", betId: "bet-1" });
    expect(state.panels[0].status).toBe("cashing_out");

    state = reduceBetsEvent(state, roundCrashed("round-41"));
    const settled = reduceBetsEvent(state, betSettled("bet-1", "round-41"));
    expect(settled.panels[0] satisfies PanelState).toMatchObject({
      status: "lost",
      request: "none",
      betId: "bet-1",
      payout: null,
    });
    // The stake was debited at the acceptance: the loss keeps the balance.
    expect(settled.balance).toBe(money("2990.00"));
    expect(
      betPanelView({
        panel: settled.panels[0],
        draft: draft("10,00"),
        limits,
        phase: "crashed",
        multiplier: 1.5,
      }).result,
    ).toBe("Perdeu");
  });
});
