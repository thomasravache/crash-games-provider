// One bet panel of the Stitch screen "Pipa Crash - Jogo da Pipa" (SPEC-0026 §6): amount with − / + (step R$ 1,00),
// chips 1, 5, 10, 50, automatic cash-out (switch + target ≥ 1,01x) and the APOSTAR (green) / CANCELAR / RETIRAR
// (orange) / PRÓXIMA button. Each panel is an instance of this component; it only sends intents, the state comes from
// the server through the bets store.

import type { BetDraft, BetLimits, PanelState, RoundPhase } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

import { notImplemented } from "./not-implemented";

export interface BetPanelProps {
  readonly panel: PanelState;
  /** What the player set (controlled: the app keeps one draft per panel). */
  readonly draft: BetDraft;
  readonly limits: BetLimits | null;
  readonly phase: RoundPhase;
  /** Current multiplier of the round, for the RETIRAR preview. */
  readonly multiplier: number;
  readonly onDraftChange: (draft: BetDraft) => void;
  readonly onPlaceBet: () => void;
  readonly onCancel: () => void;
  readonly onCashOut: () => void;
}

export function BetPanel(props: BetPanelProps): JSX.Element {
  return notImplemented(props);
}
