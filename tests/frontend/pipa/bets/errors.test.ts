import { BetError, hubErrorCode } from "@apogeu/game-sdk";
import {
  BetPanel,
  betPanelView,
  betTestIds,
  type BetPanelProps,
} from "@apogeu/game-ui";
import {
  betErrorCodes,
  betErrorMessage,
  betErrorMessages,
  contractBetErrorCodes,
  initialBetsState,
  isBetErrorCode,
  reduceBetsEvent,
  toBetErrorCode,
  type BetErrorCode,
  type PanelState,
} from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import { bettingEvent, flyingEvent } from "../support/rounds";
import { draft, limits, money } from "./support/bets";
import { startedRig } from "./support/bets-rig";
import { hubError } from "./support/fake-bets-hub";
import { render, textOf } from "./support/react";

// SPEC-0026 UT-05: each error code of the contract (and, by the 2026-10-11 amendment, every code of the consumed
// contracts plus network and unknown) shows its pt-BR message in the panel.

/** Messages fixed by the SPEC-0026 §6 contract text. */
const contractMessages: Partial<Record<BetErrorCode, string>> = {
  CASHOUT_TOO_LATE: "Cash-out tardio: a pipa já tinha sido cortada",
};

const t0 = Date.parse("2026-10-11T12:00:00.000Z");

describe("SPEC-0026 mensagens de erro do painel", () => {
  test("SPEC-0026:UT-05 cada código tem mensagem pt-BR própria; código desconhecido vira a mensagem genérica", () => {
    expect(contractBetErrorCodes).toEqual([
      "BET_BELOW_MIN",
      "BET_ABOVE_MAX",
      "INSUFFICIENT_FUNDS",
      "PANEL_BUSY",
      "CANCEL_NOT_ALLOWED",
      "CASHOUT_TOO_LATE",
      "WALLET_UNAVAILABLE",
      "INVALID_PANEL",
      "ROUND_CLOSED",
      "BET_NOT_ACTIVE",
      "REAL_MONEY_DISABLED",
    ]);
    for (const code of betErrorCodes) {
      expect(isBetErrorCode(code), code).toBe(true);
      expect(toBetErrorCode(code), code).toBe(code);
      expect(betErrorMessage(code), code).toBe(betErrorMessages[code]);
      expect(betErrorMessage(code).trim(), code).not.toBe("");
    }
    for (const [code, message] of Object.entries(contractMessages)) {
      expect(betErrorMessage(code)).toBe(message);
    }
    expect(
      new Set(betErrorCodes.map((code) => betErrorMessages[code])).size,
    ).toBe(betErrorCodes.length);

    expect(isBetErrorCode("SOMETHING_NEW")).toBe(false);
    expect(toBetErrorCode("SOMETHING_NEW")).toBe("UNKNOWN");
    expect(toBetErrorCode(undefined)).toBe("UNKNOWN");
    expect(betErrorMessage("SOMETHING_NEW")).toBe(betErrorMessages.UNKNOWN);
  });

  test("SPEC-0026:UT-05 o código vem da HubException embrulhada pelo cliente SignalR", () => {
    expect(hubErrorCode(hubError("PlaceBet", "BET_BELOW_MIN"))).toBe(
      "BET_BELOW_MIN",
    );
    expect(hubErrorCode(hubError("CashOut", "CASHOUT_TOO_LATE"))).toBe(
      "CASHOUT_TOO_LATE",
    );
    expect(
      hubErrorCode(new Error("WebSocket closed with status code: 1006")),
    ).toBeUndefined();
    expect(hubErrorCode("not an error")).toBeUndefined();
  });

  test("SPEC-0026:UT-05 requestFailed mostra a mensagem no painel (view e role=alert) e some ao dispensar", () => {
    const idle = initialBetsState.panels[0];
    for (const code of betErrorCodes) {
      const state = reduceBetsEvent(initialBetsState, {
        type: "requestFailed",
        panel: 1,
        code,
      });
      const panel = state.panels[0];
      expect(panel.error, code).toBe(code);
      expect(
        betPanelView({
          panel,
          draft: draft("10,00"),
          limits,
          phase: "betting",
          multiplier: 1,
        }).error,
        code,
      ).toBe(betErrorMessages[code]);
    }
    const failed = reduceBetsEvent(initialBetsState, {
      type: "requestFailed",
      panel: 2,
      code: "INSUFFICIENT_FUNDS",
    });
    expect(failed.panels[0]).toEqual(idle);
    expect(
      reduceBetsEvent(failed, { type: "errorDismissed", panel: 2 }).panels[1]
        .error,
    ).toBeNull();

    const props: BetPanelProps = {
      panel: { ...idle, error: "WALLET_UNAVAILABLE" } satisfies PanelState,
      draft: draft("10,00"),
      limits,
      phase: "betting",
      multiplier: 1,
      onDraftChange: () => undefined,
      onPlaceBet: () => undefined,
      onCancel: () => undefined,
      onCashOut: () => undefined,
    };
    const markup = render(BetPanel, props);
    expect(textOf(markup, betTestIds.error(1))).toBe(
      betErrorMessages.WALLET_UNAVAILABLE,
    );
    expect(markup).toMatch(
      new RegExp(
        `role="alert"[^>]*data-testid="${betTestIds.error(1)}"|data-testid="${betTestIds.error(1)}"[^>]*role="alert"`,
        "u",
      ),
    );
  });

  test("SPEC-0026:UT-05 cada código do contrato recusado pelo hub chega ao painel e ao BetError", async () => {
    const rig = await startedRig("3000.00");
    rig.round(bettingEvent(41, t0 + 6_000));
    for (const code of contractBetErrorCodes) {
      rig.hub.respond("PlaceBet", () =>
        Promise.reject(hubError("PlaceBet", code)),
      );
      const error = await rig.client
        .placeBet(1, { amount: money("10.00"), autoCashOut: null })
        .then(
          () => undefined,
          (reason: unknown) => reason,
        );
      expect(error, code).toBeInstanceOf(BetError);
      expect((error as BetError).code, code).toBe(code);
      expect(rig.state().panels[0], code).toMatchObject({
        status: "idle",
        request: "none",
        error: code,
      });
      expect(rig.state().balance, code).toBe("3000.00");
    }

    rig.hub.respond("PlaceBet", () =>
      Promise.reject(new Error("WebSocket closed with status code: 1006")),
    );
    const network = await rig.client
      .placeBet(2, { amount: money("10.00"), autoCashOut: null })
      .then(
        () => undefined,
        (reason: unknown) => reason,
      );
    expect(network).toBeInstanceOf(BetError);
    expect(["NETWORK", "UNKNOWN"]).toContain((network as BetError).code);
    await rig.client.stop();
  });

  test("SPEC-0026:UT-05 CashOutRejected CASHOUT_TOO_LATE: painel perdido com a mensagem de cash-out tardio", async () => {
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
    rig.round(flyingEvent(41, t0 + 6_000));
    rig.hub.respond("CashOut", () =>
      Promise.resolve({ cashedOut: null, pending: { betId: "bet-1" } }),
    );
    await rig.client.cashOut(1);
    expect(rig.state().panels[0].status).toBe("cashing_out");

    rig.hub.emit("CashOutRejected", {
      betId: "bet-1",
      code: "CASHOUT_TOO_LATE",
    });
    await rig.until((state) => state.panels[0].status === "lost");
    const panel = rig.state().panels[0];
    expect(panel.error).toBe("CASHOUT_TOO_LATE");
    expect(
      betPanelView({
        panel,
        draft: draft("10,00"),
        limits,
        phase: "crashed",
        multiplier: 1.2,
      }),
    ).toMatchObject({
      result: "Perdeu",
      error: "Cash-out tardio: a pipa já tinha sido cortada",
    });
    expect(rig.state().balance).toBe("2990.00");
    await rig.client.stop();
  });
});
