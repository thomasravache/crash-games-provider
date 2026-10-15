// "Minhas apostas" (SPEC-0031): the player's bets (status cashed_out, lost, cancelled, refunded…) from the history
// store of game-sdk, with "Carregar mais" while there is a next page.
//
// SCAFFOLD (SPEC-0031): signature only; rendered in the green phase.

import type { MyBetsState } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

export interface MyBetsTabProps {
  readonly state: MyBetsState;
  /** "Carregar mais": the next page by the cursor (BetsClient.loadMoreMyBets). */
  readonly onLoadMore: () => void;
}

export function MyBetsTab({ state, onLoadMore }: MyBetsTabProps): JSX.Element {
  throw new Error(
    `SPEC-0031: MyBetsTab is not implemented yet (${String(state.items.length)} bets, ${onLoadMore.name})`,
  );
}
