import type { HistoryEntry } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

import { formatMultiplier } from "./format";
import { hudText } from "./hud-text";

/** Pill colours by tier (Stitch "Histórico de Velas"): < 2x, 2x–9.99x, ≥ 10x. */
function tierClass(crashPoint: string): string {
  const value = Number(crashPoint);
  if (value >= 10) return "text-hud-tier-high bg-hud-tier-high/15 border-hud-tier-high/40";
  if (value >= 2) return "text-hud-tier-mid bg-hud-tier-mid/15 border-hud-tier-mid/40";
  return "text-hud-tier-low bg-hud-tier-low/10 border-hud-tier-low/30";
}

/**
 * Recent crash points, newest first. Each item holds only the multiplier text. The strip scrolls sideways on narrow
 * screens, so it takes keyboard focus (WCAG 2.1.1: a scrollable region must be reachable without a pointer).
 */
export function HistoryStrip({
  history,
}: {
  readonly history: readonly HistoryEntry[];
}): JSX.Element {
  return (
    <ul
      aria-label={hudText.historyLabel}
      // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- scrollable region: focus is its keyboard access (axe scrollable-region-focusable)
      tabIndex={0}
      className="flex h-9 min-w-0 items-center gap-2 overflow-x-auto bg-hud-surface px-3 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-hud-accent"
    >
      {history.map((entry) => (
        <li
          key={entry.roundId}
          className={`shrink-0 rounded-full border px-2.5 py-0.5 font-hud-display text-xs font-semibold tabular-nums ${tierClass(entry.crashPoint)}`}
        >
          {formatMultiplier(entry.crashPoint)}
        </li>
      ))}
    </ul>
  );
}
