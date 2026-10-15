// "Todas as apostas" (SPEC-0031): the bets of the sala in the round (anonymized nickname, amount, multiplier and payout
// of who cashed out) and the round totals, from the live bets store of game-sdk.

import { formatMoney, type LiveBetsState } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

import { formatMultiplier } from "../format";
import { betsTabsTestIds, betsTabsText } from "./bets-tabs-text";

export interface AllBetsTabProps {
  readonly state: LiveBetsState;
}

const columns =
  "grid grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)_minmax(0,0.8fr)_minmax(0,1fr)] gap-2";

export function AllBetsTab({ state }: AllBetsTabProps): JSX.Element {
  const { totals, liveColumns } = betsTabsText;
  return (
    <div className="flex flex-col gap-2">
      <dl className="grid grid-cols-3 gap-2 text-xs">
        <div className="flex flex-col rounded-lg bg-hud-canvas/40 px-2 py-1">
          <dt className="text-hud-muted">{totals.bets}</dt>
          <dd data-testid={betsTabsTestIds.liveTotalBets} className="font-semibold tabular-nums">
            {String(state.totalBets)}
          </dd>
        </div>
        <div className="flex flex-col rounded-lg bg-hud-canvas/40 px-2 py-1">
          <dt className="text-hud-muted">{totals.players}</dt>
          <dd data-testid={betsTabsTestIds.liveTotalPlayers} className="font-semibold tabular-nums">
            {String(state.totalPlayers)}
          </dd>
        </div>
        <div className="flex flex-col rounded-lg bg-hud-canvas/40 px-2 py-1">
          <dt className="text-hud-muted">{totals.wagered}</dt>
          <dd data-testid={betsTabsTestIds.liveTotalWagered} className="font-semibold tabular-nums">
            {state.totalWagered === null ? betsTabsText.none : formatMoney(state.totalWagered)}
          </dd>
        </div>
      </dl>

      {state.bets.length === 0 ? (
        <p className="py-2 text-center text-xs text-hud-muted">{betsTabsText.liveEmpty}</p>
      ) : (
        <>
          <div aria-hidden="true" className={`${columns} px-2 text-xs text-hud-muted`}>
            <span>{liveColumns.player}</span>
            <span className="text-right">{liveColumns.amount}</span>
            <span className="text-right">{liveColumns.multiplier}</span>
            <span className="text-right">{liveColumns.payout}</span>
          </div>
          <ul className="flex max-h-72 flex-col gap-1 overflow-y-auto">
            {state.bets.map((bet) => {
              const cashed = bet.payout !== null;
              return (
                <li
                  key={bet.betRef}
                  data-testid={betsTabsTestIds.liveBet(bet.betRef)}
                  className={`${columns} items-center rounded-lg px-2 py-1 text-sm tabular-nums ${cashed ? "bg-hud-win/10" : "bg-hud-canvas/40"}`}
                >
                  <span className="truncate">{bet.nickname}</span>
                  <span className="text-right">
                    <span className="sr-only">{`${liveColumns.amount}: `}</span>
                    {formatMoney(bet.amount)}
                  </span>
                  <span
                    className={`text-right ${cashed ? "font-semibold text-hud-win" : "text-hud-muted"}`}
                  >
                    <span className="sr-only">{`${liveColumns.multiplier}: `}</span>
                    {bet.multiplier === null ? betsTabsText.none : formatMultiplier(bet.multiplier)}
                  </span>
                  <span
                    className={`text-right ${cashed ? "font-semibold text-hud-win" : "text-hud-muted"}`}
                  >
                    <span className="sr-only">{`${liveColumns.payout}: `}</span>
                    {bet.payout === null ? betsTabsText.none : formatMoney(bet.payout)}
                  </span>
                </li>
              );
            })}
          </ul>
        </>
      )}
    </div>
  );
}
