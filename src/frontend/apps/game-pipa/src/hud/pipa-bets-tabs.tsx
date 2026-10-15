// The Pipa bets tabs (SPEC-0031) below the panels, bound to the BetsClient of game-sdk: "Todas as apostas" follows the
// live bets store (JoinSalaBets and LiveBetsBatch); "Minhas apostas" reads the first page of the history each time it
// opens, again when a bet of the player ends while it is open, and the next pages with "Carregar mais".

import type { BetsClient } from "@apogeu/game-sdk";
import type { PanelStatus } from "@apogeu/game-sdk/state";
import { BetsTabs, useLiveBetsState, useMyBetsState, type BetsTabKey } from "@apogeu/game-ui";
import { useEffect, useState, type JSX } from "react";

export interface PipaBetsTabsProps {
  readonly client: BetsClient;
}

/** Panel statuses of a bet that ended (it is in the history with its outcome). */
const finished: ReadonlySet<PanelStatus> = new Set<PanelStatus>(["cashed_out", "lost", "refunded"]);

/** Slot `betsTabs` of the Hud. */
export function PipaBetsTabs({ client }: PipaBetsTabsProps): JSX.Element {
  const live = useLiveBetsState(client.liveBets);
  const mine = useMyBetsState(client.myBets);
  const [selected, setSelected] = useState<BetsTabKey>("all");

  useEffect(() => {
    if (selected !== "mine") return undefined;
    // A failure is shown by the tab (its error and "Tentar de novo"); nothing else to do here.
    const read = (): void => {
      client.loadMyBets().catch(() => undefined);
    };
    read();
    let previous = client.store.getState().panels;
    return client.store.subscribe(() => {
      const next = client.store.getState().panels;
      const ended = next.some(
        (panel, index) => finished.has(panel.status) && previous[index]?.status !== panel.status,
      );
      previous = next;
      if (ended) read();
    });
  }, [client, selected]);

  return (
    <BetsTabs
      selected={selected}
      onSelect={setSelected}
      live={live}
      mine={mine}
      onLoadMore={() => {
        client.loadMoreMyBets().catch(() => undefined);
      }}
      onRetry={() => {
        client.loadMyBets().catch(() => undefined);
      }}
    />
  );
}
