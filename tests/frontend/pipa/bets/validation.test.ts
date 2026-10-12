import { betPanelView } from "@apogeu/game-ui";
import {
  amountStep,
  betValidationMessage,
  initialBetsState,
  minAutoCashOut,
  quickChips,
  stepAmount,
  validateBet,
  type BetValidation,
} from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import { draft, limits, money } from "./support/bets";

// SPEC-0026 UT-02: with limits R$ 1,00 / R$ 1.000,00 and the minimum automatic target 1,01x, an amount of 0,50 or
// 1.000,01, or a target of 1,00x, disables the button with the reason (pt-BR); the server still has the final word.

const reasonOf = (validation: BetValidation): string | null =>
  validation.ok ? null : validation.reason;
const messageOf = (validation: BetValidation): string | null =>
  validation.ok ? null : validation.message;

describe("SPEC-0026 validação local do painel", () => {
  test("SPEC-0026:UT-02 valor abaixo do mínimo (0,50) e acima do máximo (1.000,01) são recusados com o motivo", () => {
    const below = validateBet(draft("0,50"), limits);
    expect(reasonOf(below)).toBe("BELOW_MIN");
    expect(messageOf(below)).toContain("R$ 1,00");

    for (const amount of ["1000,01", "1000.01", "R$ 1.000,01"]) {
      const above = validateBet(draft(amount), limits);
      expect(reasonOf(above), amount).toBe("ABOVE_MAX");
      expect(messageOf(above), amount).toContain("R$ 1.000,00");
    }
  });

  test("SPEC-0026:UT-02 os limites são inclusivos e valores válidos passam (pt-BR ou decimal)", () => {
    for (const amount of [
      "1,00",
      "1.00",
      "10,50",
      "R$ 10,50",
      "1000,00",
      "1.000,00",
    ]) {
      expect(validateBet(draft(amount), limits), amount).toEqual({ ok: true });
    }
  });

  test("SPEC-0026:UT-02 alvo do cash-out automático de 1,00x é recusado; 1,01x passa; desligado não é conferido", () => {
    expect(minAutoCashOut).toBe("1.01");
    const low = validateBet(draft("10,00", "1,00"), limits);
    expect(reasonOf(low)).toBe("AUTO_TARGET_TOO_LOW");
    expect(messageOf(low)).toContain("1,01x");
    expect(reasonOf(validateBet(draft("10,00", "abc"), limits))).toBe(
      "AUTO_TARGET_INVALID",
    );
    expect(validateBet(draft("10,00", "1,01"), limits)).toEqual({ ok: true });
    expect(validateBet(draft("10,00", "2,50x"), limits)).toEqual({ ok: true });
    expect(
      validateBet(
        {
          amount: "10,00",
          autoCashOutEnabled: false,
          autoCashOutTarget: "1,00",
        },
        limits,
      ),
    ).toEqual({ ok: true });
  });

  test("SPEC-0026:UT-02 valor ilegível ou limites ainda desconhecidos desabilitam o botão", () => {
    for (const amount of ["", "abc", "-5", "10,505"]) {
      expect(reasonOf(validateBet(draft(amount), limits)), amount).toBe(
        "INVALID_AMOUNT",
      );
    }
    expect(reasonOf(validateBet(draft("10,00"), null))).toBe("LIMITS_UNKNOWN");
    expect(betValidationMessage("BELOW_MIN", limits)).toBe(
      messageOf(validateBet(draft("0,50"), limits)),
    );
  });

  test("SPEC-0026:UT-02 o painel idle fica desabilitado com o motivo e habilita com valor válido", () => {
    const idle = initialBetsState.panels[0];
    for (const [input, reason] of [
      [draft("0,50"), "BELOW_MIN"],
      [draft("1000,01"), "ABOVE_MAX"],
      [draft("10,00", "1,00"), "AUTO_TARGET_TOO_LOW"],
    ] as const) {
      const shown = betPanelView({
        panel: idle,
        draft: input,
        limits,
        phase: "betting",
        multiplier: 1,
      });
      expect(shown.action, reason).toBe("bet");
      expect(shown.disabled, reason).toBe(true);
      expect(shown.disabledReason, reason).toBe(
        betValidationMessage(reason, limits),
      );
    }
    const valid = betPanelView({
      panel: idle,
      draft: draft("10,00"),
      limits,
      phase: "betting",
      multiplier: 1,
    });
    expect(valid).toMatchObject({ disabled: false, disabledReason: null });
  });

  test("SPEC-0026:UT-02 − / + andam R$ 1,00 dentro dos limites; fichas 1, 5, 10 e 50", () => {
    expect(amountStep).toBe("1.00");
    expect(quickChips).toEqual(["1.00", "5.00", "10.00", "50.00"]);
    expect(stepAmount(money("10.00"), 1, limits)).toBe("11.00");
    expect(stepAmount(money("10.50"), -1, limits)).toBe("9.50");
    expect(stepAmount(money("1.00"), -1, limits)).toBe("1.00");
    expect(stepAmount(money("1000.00"), 1, limits)).toBe("1000.00");
    expect(stepAmount(money("0.50"), 1, limits)).toBe("1.50");
  });
});
