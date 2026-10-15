// "Todas as apostas" (SPEC-0031): the bets of the sala in the round (anonymized nickname, amount, multiplier and payout
// of who cashed out) and the round totals, from the live bets store of game-sdk.
//
// SCAFFOLD (SPEC-0031): signature only; rendered in the green phase.

import type { LiveBetsState } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

export interface AllBetsTabProps {
  readonly state: LiveBetsState;
}

export function AllBetsTab({ state }: AllBetsTabProps): JSX.Element {
  throw new Error(
    `SPEC-0031: AllBetsTab is not implemented yet (${String(state.bets.length)} bets)`,
  );
}
