// What one bet panel shows for its state (SPEC-0026 §6): button, label, disabled reason, result and error. Pure, so the
// panel states, the local validation and the error messages are tested without a DOM (UT-01, UT-02, UT-05).

import {
  betErrorMessage,
  cashOutPreview,
  formatMoney,
  parseMoneyInput,
  validateBet,
  type BetDraft,
  type BetLimits,
  type Money,
  type Multiplier,
  type PanelState,
  type RoundPhase,
} from "@apogeu/game-sdk/state";

import { betPanelText } from "./bet-text";

/** What the main button does: bet (idle), cancel (placed/queued), cash out (active), nothing (finished/waiting). */
export type BetPanelAction = "bet" | "cancel" | "cashOut" | "none";

export interface BetPanelViewInput {
  readonly panel: PanelState;
  readonly draft: BetDraft;
  readonly limits: BetLimits | null;
  readonly phase: RoundPhase;
  /** Current multiplier of the round (RoundState.multiplier) for the "RETIRAR R$ x" preview. */
  readonly multiplier: number;
}

export interface BetPanelView {
  /** "APOSTA 1" / "APOSTA 2". */
  readonly title: string;
  readonly action: BetPanelAction;
  /** "APOSTAR R$ 10,00", "CANCELAR", "RETIRAR R$ 24,70", "Retirando…". */
  readonly label: string;
  /** "PRÓXIMA" while queued; null otherwise. */
  readonly badge: string | null;
  readonly disabled: boolean;
  /**
   * A request of the panel is waiting for the server (placing, cancelling, "Retirando…"): the button stays focusable
   * and announces aria-disabled instead of losing the focus to the disabled attribute (UT-10).
   */
  readonly busy: boolean;
  /** pt-BR reason of a disabled button (local validation); null when enabled or busy. */
  readonly disabledReason: string | null;
  /** "Ganhou R$ y", "Perdeu", "Estornado R$ x"; null while there is no result. */
  readonly result: string | null;
  /** "Crédito em processamento" while the credit is pending; null otherwise. */
  readonly note: string | null;
  /** pt-BR message of the panel's last server error; null when none. */
  readonly error: string | null;
  /** Whether the amount, chips and automatic cash-out controls can be changed (idle only). */
  readonly editable: boolean;
}

/** The interpolated multiplier of the round as a two-place decimal string, floored (2.479 → "2.47"). */
export function multiplierOf(value: number): Multiplier {
  // The epsilon keeps values like 1.13 (1.12999… as a double) on their own hundredth.
  const hundredths = Math.max(100, Math.floor(value * 100 + 1e-7));
  return (hundredths / 100).toFixed(2) as Multiplier;
}

function resultOf(panel: PanelState): string | null {
  switch (panel.status) {
    case "cashed_out":
      return panel.payout === null ? null : betPanelText.won(formatMoney(panel.payout));
    case "lost":
      return betPanelText.lost;
    case "refunded":
      return panel.refunded === null ? null : betPanelText.refunded(formatMoney(panel.refunded));
    case "idle":
    case "queued":
    case "placed":
    case "active":
    case "cashing_out":
      return null;
  }
}

function cashOutLabel(amount: Money | null, multiplier: number, limits: BetLimits | null): string {
  if (amount === null) return betPanelText.cashOut("").trim();
  const preview = cashOutPreview(amount, multiplierOf(multiplier), limits?.maxWin);
  return betPanelText.cashOut(formatMoney(preview));
}

export function betPanelView(input: BetPanelViewInput): BetPanelView {
  const { panel, draft, limits } = input;
  const busy = panel.request !== "none" || panel.status === "cashing_out";
  const common = {
    busy,
    title: betPanelText.title(panel.panel),
    badge: null,
    disabledReason: null,
    result: resultOf(panel),
    note:
      panel.creditStatus === "pending" &&
      (panel.status === "cashed_out" || panel.status === "refunded")
        ? betPanelText.creditPending
        : null,
    error: panel.error === null ? null : betErrorMessage(panel.error),
    editable: false,
  } as const;

  switch (panel.status) {
    case "idle":
    case "cashed_out":
    case "lost":
    case "refunded": {
      const amount = parseMoneyInput(draft.amount);
      const validation = validateBet(draft, limits);
      return {
        ...common,
        action: "bet",
        label: betPanelText.bet(amount === null ? "" : formatMoney(amount)).trim(),
        disabled: busy || !validation.ok,
        disabledReason: busy || validation.ok ? null : validation.message,
        editable: !busy,
      };
    }
    case "placed":
    case "queued":
      return {
        ...common,
        action: "cancel",
        label: betPanelText.cancel,
        badge: panel.status === "queued" ? betPanelText.next : null,
        disabled: busy,
      };
    case "active":
      // The round is over and the outcome (BetSettled, CashedOut) has not arrived: no RETIRAR (UT-08).
      if (input.phase === "crashed" || input.phase === "annulled") {
        return { ...common, action: "none", label: betPanelText.settling, disabled: true };
      }
      return {
        ...common,
        action: "cashOut",
        label: cashOutLabel(panel.amount, input.multiplier, limits),
        disabled: busy || panel.amount === null,
      };
    case "cashing_out":
      return { ...common, action: "none", label: betPanelText.cashingOut, disabled: true };
  }
}
