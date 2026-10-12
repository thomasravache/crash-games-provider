// aria-live announcement of a panel transition (SPEC-0026 §2, WCAG 2.2 AA): bet accepted, cash-out (manual or
// automatic) and loss or refund. Pure: the app keeps the previous panels and announces the change.

import { formatMoney, type PanelState } from "@apogeu/game-sdk/state";

import { betAnnouncement } from "./bet-text";
import { formatMultiplier } from "../format";

/** What to announce when a panel goes from `previous` to `next`; null when nothing worth saying happened. */
export function betAnnouncementFor(previous: PanelState, next: PanelState): string | null {
  if (previous.status === next.status) return null;
  const amount = next.amount === null ? null : formatMoney(next.amount);
  switch (next.status) {
    case "placed":
      return previous.status === "queued" || amount === null
        ? null
        : betAnnouncement.accepted(next.panel, amount);
    case "queued":
      return amount === null ? null : betAnnouncement.queued(next.panel, amount);
    case "cashed_out": {
      if (next.payout === null || next.multiplier === null) return null;
      const payout = formatMoney(next.payout);
      const multiplier = formatMultiplier(next.multiplier);
      return next.auto
        ? betAnnouncement.autoCashedOut(next.panel, payout, multiplier)
        : betAnnouncement.cashedOut(next.panel, payout, multiplier);
    }
    case "lost":
      return betAnnouncement.lost(next.panel);
    case "refunded":
      return next.refunded === null
        ? null
        : betAnnouncement.refunded(next.panel, formatMoney(next.refunded));
    case "idle":
    case "active":
    case "cashing_out":
      return null;
  }
}
