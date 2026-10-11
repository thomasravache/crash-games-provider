// Bets store without a framework (SPEC-0026 §4), like the round store: subscribable, consumed by React through
// useSyncExternalStore. It only mirrors what the server confirmed: the balance changes only with server values.

import { toBetErrorCode } from "./bet-errors";
import type { BetsEvent } from "./bets-events";
import type {
  BetsState,
  BetsStateSource,
  OpenBet,
  PanelNumber,
  PanelState,
  PanelStatus,
} from "./bets-state";

export interface BetsStore extends BetsStateSource {
  /** Reduces one event; stale, duplicated or unknown-bet events leave the state unchanged. */
  dispatch(event: BetsEvent): void;
}

/** Idle panel (no bet, no error). */
export function idlePanel(panel: PanelNumber): PanelState {
  return {
    panel,
    status: "idle",
    request: "none",
    betId: null,
    clientBetId: null,
    roundId: null,
    amount: null,
    autoCashOut: null,
    multiplier: null,
    payout: null,
    auto: false,
    creditStatus: null,
    refunded: null,
    error: null,
  };
}

export const initialBetsState: BetsState = {
  panels: [idlePanel(1), idlePanel(2)],
  balance: null,
  currency: null,
  limits: null,
};

/** Panel states that show a finished bet until the next betting phase. */
const finishedStatuses: readonly PanelStatus[] = ["cashed_out", "lost", "refunded"];

/** Panel states from which the player may place a new bet. */
const bettableStatuses: readonly PanelStatus[] = ["idle", ...finishedStatuses];

type PanelUpdate = (panel: PanelState) => PanelState;

/** Applies `update` to the panels matching `matches`; the same state when nothing changed. */
function updatePanels(
  state: BetsState,
  matches: (panel: PanelState) => boolean,
  update: PanelUpdate,
  extra?: Partial<Pick<BetsState, "balance" | "currency">>,
): BetsState {
  const [current1, current2] = state.panels;
  const match1 = matches(current1);
  const match2 = matches(current2);
  if (!match1 && !match2) return state;
  const first = match1 ? update(current1) : current1;
  const second = match2 ? update(current2) : current2;
  return { ...state, ...extra, panels: [first, second] };
}

const byNumber =
  (panel: PanelNumber) =>
  (candidate: PanelState): boolean =>
    candidate.panel === panel;

const byBet =
  (betId: string, statuses: readonly PanelStatus[]) =>
  (candidate: PanelState): boolean =>
    candidate.betId === betId && statuses.includes(candidate.status);

/** The server balance of a message, when it carries one. */
const withBalance = (balance: BetsState["balance"]): Partial<Pick<BetsState, "balance">> =>
  balance === null ? {} : { balance };

function fromOpenBet(panel: PanelState, bet: OpenBet): PanelState {
  return {
    ...idlePanel(panel.panel),
    status: bet.status,
    betId: bet.betId,
    clientBetId: panel.betId === bet.betId ? panel.clientBetId : null,
    roundId: bet.roundId,
    amount: bet.amount,
    autoCashOut: bet.autoCashOut,
  };
}

/** Next state for one event; returns the same object when the event must be ignored. */
export function reduceBetsEvent(state: BetsState, event: BetsEvent): BetsState {
  switch (event.type) {
    case "limits":
      return { ...state, limits: event.limits };

    case "balance":
    case "balanceUpdated": {
      const confirmed = updatePanels(
        state,
        (panel) => panel.creditStatus === "pending",
        (panel) => ({ ...panel, creditStatus: "credited" }),
      );
      return { ...confirmed, balance: event.balance, currency: event.currency };
    }

    case "reloaded": {
      const base: BetsState = { ...state, balance: event.balance, currency: event.currency };
      const openBets = event.openBets;
      if (openBets === null) return base;
      const reload = (panel: PanelState): PanelState => {
        const bet = openBets.find((candidate) => candidate.panel === panel.panel);
        if (bet === undefined) return panel.status === "idle" ? panel : idlePanel(panel.panel);
        return fromOpenBet(panel, bet);
      };
      return { ...base, panels: [reload(state.panels[0]), reload(state.panels[1])] };
    }

    case "placeRequested":
      return updatePanels(
        state,
        (panel) =>
          panel.panel === event.panel &&
          panel.request === "none" &&
          bettableStatuses.includes(panel.status),
        (panel) => ({
          ...idlePanel(panel.panel),
          request: "placing",
          clientBetId: event.clientBetId,
          amount: event.amount,
          autoCashOut: event.autoCashOut,
        }),
      );

    case "betAccepted":
      return updatePanels(
        state,
        (panel) =>
          panel.panel === event.panel &&
          panel.clientBetId === event.clientBetId &&
          panel.betId === null,
        (panel) => ({
          ...panel,
          status: event.status,
          request: "none",
          betId: event.betId,
          roundId: event.roundId,
          amount: event.amount,
          autoCashOut: event.autoCashOut,
          error: null,
        }),
        { balance: event.balance },
      );

    case "betActivated":
      return updatePanels(state, byBet(event.betId, ["queued"]), (panel) => ({
        ...panel,
        status: "placed",
        roundId: event.roundId,
      }));

    case "cancelRequested":
      return updatePanels(
        state,
        (panel) =>
          panel.panel === event.panel &&
          panel.request === "none" &&
          (panel.status === "placed" || panel.status === "queued"),
        (panel) => ({ ...panel, request: "cancelling", error: null }),
      );

    case "betCancelled":
      return updatePanels(
        state,
        byBet(event.betId, ["placed", "queued"]),
        (panel) => idlePanel(panel.panel),
        withBalance(event.balance),
      );

    case "cashOutRequested":
      return updatePanels(
        state,
        (panel) =>
          panel.panel === event.panel && panel.request === "none" && panel.status === "active",
        (panel) => ({ ...panel, request: "cashing_out", error: null }),
      );

    case "cashOutPending":
      return updatePanels(state, byBet(event.betId, ["active"]), (panel) => ({
        ...panel,
        status: "cashing_out",
        request: "none",
      }));

    case "cashedOut":
      return updatePanels(
        state,
        byBet(event.betId, ["active", "cashing_out", "lost"]),
        (panel) => ({
          ...panel,
          status: "cashed_out",
          request: "none",
          multiplier: event.multiplier,
          payout: event.payout,
          auto: event.auto,
          creditStatus: event.creditStatus,
          error: null,
        }),
        withBalance(event.balance),
      );

    case "cashOutRejected":
      return updatePanels(state, byBet(event.betId, ["active", "cashing_out"]), (panel) => ({
        ...panel,
        status: "lost",
        request: "none",
        multiplier: null,
        payout: null,
        error: toBetErrorCode(event.code),
      }));

    case "betSettled":
      return updatePanels(state, byBet(event.betId, ["placed", "active"]), (panel) => ({
        ...panel,
        status: "lost",
        request: "none",
      }));

    case "betRefunded":
      return updatePanels(
        state,
        byBet(event.betId, ["queued", "placed", "active", "cashing_out"]),
        (panel) => ({
          ...panel,
          status: "refunded",
          request: "none",
          refunded: event.refunded,
          creditStatus: event.refundStatus === "pending" ? "pending" : "credited",
        }),
        withBalance(event.balance),
      );

    case "requestFailed":
      return updatePanels(state, byNumber(event.panel), (panel) =>
        panel.status === "idle"
          ? { ...idlePanel(panel.panel), error: event.code }
          : { ...panel, request: "none", error: event.code },
      );

    case "errorDismissed":
      return updatePanels(
        state,
        (panel) => panel.panel === event.panel && panel.error !== null,
        (panel) => ({ ...panel, error: null }),
      );

    case "roundBetting":
      return updatePanels(
        state,
        (panel) => finishedStatuses.includes(panel.status) && panel.request === "none",
        (panel) => idlePanel(panel.panel),
      );

    case "roundFlying":
      return updatePanels(
        state,
        (panel) => panel.status === "placed" && panel.roundId === event.roundId,
        (panel) => ({ ...panel, status: "active" }),
      );

    case "roundCrashed":
    case "roundAnnulled":
      // The outcome comes from the server: BetSettled, CashedOut or BetRefunded.
      return state;
  }
}

/** Panel of a state by number. */
export function panelOf(state: BetsState, panel: PanelNumber): PanelState {
  return panel === 1 ? state.panels[0] : state.panels[1];
}

/** Store with the initial state, or `initial` (tests). */
export function createBetsStore(initial: BetsState = initialBetsState): BetsStore {
  let state = initial;
  const listeners = new Set<() => void>();
  return {
    getState: () => state,
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    dispatch(event) {
      const next = reduceBetsEvent(state, event);
      if (next === state) return;
      state = next;
      for (const listener of [...listeners]) listener();
    },
  };
}
