// The bets tabs below the panels (SPEC-0031): "Todas as apostas" and "Minhas apostas", in the ARIA tabs pattern (role
// tab / tabpanel, aria-selected, roving tabindex with the arrow keys, Home and End). Controlled: the app keeps the
// selected tab and reads the history when "Minhas apostas" opens.

import type { LiveBetsState, MyBetsState } from "@apogeu/game-sdk/state";
import { useId, useRef, type JSX, type KeyboardEvent } from "react";

import { AllBetsTab } from "./all-bets-tab";
import { betsTabsTestIds, betsTabsText } from "./bets-tabs-text";
import { MyBetsTab } from "./my-bets-tab";

export type BetsTabKey = "all" | "mine";

const tabKeys: readonly BetsTabKey[] = ["all", "mine"];

export interface BetsTabsProps {
  readonly selected: BetsTabKey;
  readonly onSelect: (tab: BetsTabKey) => void;
  readonly live: LiveBetsState;
  readonly mine: MyBetsState;
  /** "Carregar mais" of "Minhas apostas". */
  readonly onLoadMore: () => void;
  /** "Tentar de novo" after a failed history read. */
  readonly onRetry?: () => void;
}

const focusRing =
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-hud-win";

export function BetsTabs({
  selected,
  onSelect,
  live,
  mine,
  onLoadMore,
  onRetry,
}: BetsTabsProps): JSX.Element {
  const id = useId();
  const tabs = useRef(new Map<BetsTabKey, HTMLButtonElement>());
  const tabId = (tab: BetsTabKey): string => `${id}-tab-${tab}`;
  const panelId = (tab: BetsTabKey): string => `${id}-panel-${tab}`;

  const move = (event: KeyboardEvent<HTMLButtonElement>): void => {
    const index = tabKeys.indexOf(selected);
    let next: BetsTabKey | undefined;
    switch (event.key) {
      case "ArrowRight":
        next = tabKeys[(index + 1) % tabKeys.length];
        break;
      case "ArrowLeft":
        next = tabKeys[(index - 1 + tabKeys.length) % tabKeys.length];
        break;
      case "Home":
        next = tabKeys[0];
        break;
      case "End":
        next = tabKeys[tabKeys.length - 1];
        break;
      default:
        return;
    }
    if (next === undefined) return;
    event.preventDefault();
    onSelect(next);
    tabs.current.get(next)?.focus();
  };

  return (
    <section className="flex flex-col gap-2 rounded-2xl border border-hud-border bg-hud-canvas/40 p-3">
      <div
        role="tablist"
        aria-label={betsTabsText.tabList}
        className="grid grid-cols-2 gap-1 rounded-xl bg-hud-canvas p-1"
      >
        {tabKeys.map((tab) => {
          const active = tab === selected;
          return (
            <button
              key={tab}
              ref={(element) => {
                if (element === null) tabs.current.delete(tab);
                else tabs.current.set(tab, element);
              }}
              type="button"
              role="tab"
              id={tabId(tab)}
              data-testid={betsTabsTestIds.tab(tab)}
              aria-selected={active}
              aria-controls={panelId(tab)}
              tabIndex={active ? 0 : -1}
              onClick={() => {
                onSelect(tab);
              }}
              onKeyDown={move}
              className={`min-h-10 rounded-lg font-hud-display text-sm font-semibold ${focusRing} ${active ? "bg-hud-surface-raised text-hud-text" : "text-hud-muted hover:text-hud-text"}`}
            >
              {betsTabsText[tab]}
            </button>
          );
        })}
      </div>
      {tabKeys.map((tab) => (
        <div
          key={tab}
          role="tabpanel"
          id={panelId(tab)}
          aria-labelledby={tabId(tab)}
          hidden={tab !== selected}
        >
          {tab !== selected ? null : tab === "all" ? (
            <AllBetsTab state={live} />
          ) : (
            <MyBetsTab
              state={mine}
              onLoadMore={onLoadMore}
              {...(onRetry === undefined ? {} : { onRetry })}
            />
          )}
        </div>
      ))}
    </section>
  );
}
