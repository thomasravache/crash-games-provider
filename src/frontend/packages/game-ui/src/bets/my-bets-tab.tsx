// "Minhas apostas" (SPEC-0031): the player's bets (status cashed_out, lost, cancelled, refunded…) from the history
// store of game-sdk, with "Carregar mais" while there is a next page.

import { formatMoney, type MyBetStatus, type MyBetsState } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

import { formatMultiplier } from "../format";
import { betsTabsTestIds, betsTabsText } from "./bets-tabs-text";

export interface MyBetsTabProps {
  readonly state: MyBetsState;
  /** "Carregar mais": the next page by the cursor (BetsClient.loadMoreMyBets). */
  readonly onLoadMore: () => void;
  /** "Tentar de novo" after a failed read; absent: "Carregar mais" retries. */
  readonly onRetry?: () => void;
}

const columns =
  "grid grid-cols-[minmax(0,0.7fr)_minmax(0,1fr)_minmax(0,0.8fr)_minmax(0,1fr)_minmax(0,1fr)] gap-2";

const statusStyles: Record<MyBetStatus, string> = {
  placed: "text-hud-betting",
  queued: "text-hud-betting",
  active: "text-hud-flying",
  cashed_out: "font-semibold text-hud-win",
  lost: "text-hud-text",
  cancelled: "text-hud-muted",
  refunded: "text-hud-muted",
};

const focusRing =
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-hud-win";

export function MyBetsTab({ state, onLoadMore, onRetry }: MyBetsTabProps): JSX.Element {
  const { mineColumns } = betsTabsText;
  if (!state.loaded && state.error === null) {
    return <p className="py-2 text-center text-xs text-hud-muted">{betsTabsText.mineLoading}</p>;
  }
  return (
    <div className="flex flex-col gap-2">
      {state.loaded && state.items.length === 0 ? (
        <p className="py-2 text-center text-xs text-hud-muted">{betsTabsText.mineEmpty}</p>
      ) : null}
      {state.items.length === 0 ? null : (
        <>
          <div aria-hidden="true" className={`${columns} px-2 text-xs text-hud-muted`}>
            <span>{mineColumns.round}</span>
            <span className="text-right">{mineColumns.amount}</span>
            <span className="text-right">{mineColumns.multiplier}</span>
            <span className="text-right">{mineColumns.payout}</span>
            <span className="text-right">{mineColumns.status}</span>
          </div>
          <ul className="flex max-h-72 flex-col gap-1 overflow-y-auto">
            {state.items.map((bet) => (
              <li
                key={bet.betId}
                data-testid={betsTabsTestIds.myBet(bet.betId)}
                className={`${columns} items-center rounded-lg px-2 py-1 text-sm tabular-nums ${bet.status === "cashed_out" ? "bg-hud-win/10" : "bg-hud-canvas/40"}`}
              >
                <span className="text-hud-muted">
                  <span className="sr-only">{`${mineColumns.round}: `}</span>
                  {betsTabsText.round(bet.roundNumber)}
                </span>
                <span className="text-right">
                  <span className="sr-only">{`${mineColumns.amount}: `}</span>
                  {formatMoney(bet.amount)}
                </span>
                <span className="text-right text-hud-muted">
                  <span className="sr-only">{`${mineColumns.multiplier}: `}</span>
                  {bet.multiplier === null ? betsTabsText.none : formatMultiplier(bet.multiplier)}
                </span>
                <span className="text-right">
                  <span className="sr-only">{`${mineColumns.payout}: `}</span>
                  {bet.payout === null ? betsTabsText.none : formatMoney(bet.payout)}
                </span>
                <span className={`text-right ${statusStyles[bet.status]}`}>
                  {betsTabsText.status[bet.status]}
                </span>
              </li>
            ))}
          </ul>
        </>
      )}
      {state.error === null ? null : (
        <div
          role="alert"
          className="flex items-center gap-2 rounded-lg border border-hud-crashed/60 px-2 py-1 text-xs"
        >
          <p className="flex-1">{betsTabsText.mineError}</p>
          <button
            type="button"
            onClick={onRetry ?? onLoadMore}
            className={`min-h-8 rounded-full border border-hud-border px-3 font-semibold hover:bg-hud-surface-raised ${focusRing}`}
          >
            {betsTabsText.retry}
          </button>
        </div>
      )}
      {state.nextCursor === null ? null : (
        <button
          type="button"
          data-testid={betsTabsTestIds.loadMore}
          disabled={state.loading}
          aria-busy={state.loading ? true : undefined}
          onClick={onLoadMore}
          className={`min-h-10 w-full rounded-lg border border-hud-border font-hud-display text-sm font-semibold text-hud-text hover:bg-hud-surface-raised aria-busy:cursor-wait aria-busy:opacity-60 ${focusRing}`}
        >
          {betsTabsText.loadMore}
        </button>
      )}
    </div>
  );
}
