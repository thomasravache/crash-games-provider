// What one bet panel shows for its state (SPEC-0026 §6): button, label, disabled reason, result and error. Pure, so the
// panel states, the local validation and the error messages are tested without a DOM (UT-01, UT-02, UT-05).

import type { BetDraft, BetLimits, PanelState, RoundPhase } from "@apogeu/game-sdk/state";

import { notImplemented } from "./not-implemented";

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

export function betPanelView(input: BetPanelViewInput): BetPanelView {
  return notImplemented(input);
}
