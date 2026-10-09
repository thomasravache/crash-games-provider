// "Comprovadamente Justo" window (SPEC-0010 §6): the player picks a finished round, the browser fetches its
// verification data and checks it with verifyRound (game-sdk), showing "Verificado" and the recomputed crash point.

import type { RoundVerificationData } from "@apogeu/game-sdk/fairness";
import type { HistoryEntry } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

import { NotImplementedError } from "./not-implemented";

export interface FairnessDialogProps {
  readonly history: readonly HistoryEntry[];
  /** GET /api/v1/rounds/{roundId}/verification. */
  readonly loadVerification: (roundId: string) => Promise<RoundVerificationData>;
  readonly onClose: () => void;
}

export function FairnessDialog(props: FairnessDialogProps): JSX.Element {
  throw new NotImplementedError("FairnessDialog", props);
}
