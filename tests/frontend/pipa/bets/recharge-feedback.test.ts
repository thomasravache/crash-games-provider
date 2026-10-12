import {
  BalanceDisplay,
  BetPanel,
  betTestIds,
  type BalanceDisplayProps,
  type BetPanelProps,
} from "@apogeu/game-ui";
import {
  betErrorMessages,
  initialBetsState,
  type BetErrorCode,
  type PanelState,
} from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import { draft, limits, money } from "./support/bets";
import { attributesOf, render } from "./support/react";

// SPEC-0026 UT-10 (amendment 2026-10-12, review G4): a refused demo recharge (RECHARGE_NOT_ALLOWED or WALLET_UNAVAILABLE)
// shows its pt-BR message with role="alert" next to the balance; and the action button of the panel keeps the keyboard
// focus while a request is in flight ("placing", "Retirando…"): aria-disabled instead of the disabled attribute.
//
// Seam: BalanceDisplay receives the code of the last refused recharge as `rechargeError` (null when none) and renders
// betErrorMessage(code) in an element with role="alert".

type RechargeErrorProps = BalanceDisplayProps & {
  readonly rechargeError: BetErrorCode | null;
};

function balanceMarkup(rechargeError: BetErrorCode | null): string {
  const props: RechargeErrorProps = {
    balance: money("0.50"),
    limits,
    recharging: false,
    onRecharge: () => undefined,
    rechargeError,
  };
  return render(BalanceDisplay, props);
}

/** Text of every element with role="alert" in the markup. */
function alerts(markup: string): string[] {
  return [
    ...markup.matchAll(/<([a-z]+)\b[^>]*role="alert"[^>]*>([\s\S]*?)<\/\1>/gu),
  ].map((found) => (found[2] ?? "").replace(/<[^>]+>/gu, "").trim());
}

function actionButton(panel: PanelState): string {
  const props: BetPanelProps = {
    panel,
    draft: draft("10,00"),
    limits,
    phase: panel.status === "idle" ? "betting" : "flying",
    multiplier: 2.47,
    onDraftChange: () => undefined,
    onPlaceBet: () => undefined,
    onCancel: () => undefined,
    onCashOut: () => undefined,
  };
  const attributes = attributesOf(
    render(BetPanel, props),
    betTestIds.action(1),
  );
  if (attributes === null) throw new Error("the action button is not rendered");
  return attributes;
}

describe("SPEC-0026 recarga recusada e foco do botão de ação", () => {
  test("SPEC-0026:UT-10 recarga recusada (RECHARGE_NOT_ALLOWED ou WALLET_UNAVAILABLE) mostra a mensagem pt-BR com role=alert", () => {
    for (const code of [
      "RECHARGE_NOT_ALLOWED",
      "WALLET_UNAVAILABLE",
    ] as const) {
      expect(alerts(balanceMarkup(code)), code).toEqual([
        betErrorMessages[code],
      ]);
    }
    expect(alerts(balanceMarkup(null))).toEqual([]);
  });

  test("SPEC-0026:UT-10 durante placing e Retirando… o botão de ação usa aria-disabled e não disabled (foco mantido)", () => {
    const idle = initialBetsState.panels[0];
    const placing: PanelState = { ...idle, request: "placing" };
    const cashingOut: PanelState = {
      ...idle,
      status: "cashing_out",
      request: "cashing_out",
      betId: "bet-1",
      roundId: "round-41",
      amount: money("10.00"),
    };
    for (const [name, panel] of [
      ["placing", placing],
      ["cashing_out", cashingOut],
    ] as const) {
      const attributes = actionButton(panel);
      expect(attributes, name).toMatch(/aria-disabled="true"/u);
      expect(attributes, name).not.toMatch(/(^|\s)disabled(=""|\s|$)/u);
    }

    // An idle panel ready to bet is enabled either way.
    expect(actionButton(idle)).not.toMatch(
      /aria-disabled="true"|(^|\s)disabled(=""|\s|$)/u,
    );
  });
});
