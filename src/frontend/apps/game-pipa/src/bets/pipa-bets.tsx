// The Pipa bet area (SPEC-0026): the two panels (APOSTA 1 / APOSTA 2) and the confirmed balance with "Recarregar demo",
// bound to the BetsClient of game-sdk. Each panel keeps its own draft (amount, automatic cash-out) and only sends
// intents; what it shows comes from the bets store. Bet accepted, cash-out and loss are announced (aria-live).

import { BetError, type BetsClient } from "@apogeu/game-sdk";
import {
  parseMoneyInput,
  parseMultiplierInput,
  type BetDraft,
  type BetErrorCode,
  type BetsState,
  type PanelNumber,
  type RoundState,
} from "@apogeu/game-sdk/state";
import { BalanceDisplay, BetPanel, betAnnouncementFor, useBetsState } from "@apogeu/game-ui";
import { useEffect, useState, type JSX } from "react";

export interface PipaBetsProps {
  readonly client: BetsClient;
  /** Round state (phase and current multiplier) the panels follow. */
  readonly round: RoundState;
}

const initialDraft: BetDraft = {
  amount: "1,00",
  autoCashOutEnabled: false,
  autoCashOutTarget: "2,00",
};

const panelNumbers = [1, 2] as const;

/** Last announcement of a panel transition, from the bets store subscription. */
function useBetAnnouncement(client: BetsClient): string {
  const [announcement, setAnnouncement] = useState("");
  useEffect(() => {
    let previous: BetsState = client.store.getState();
    return client.store.subscribe(() => {
      const next = client.store.getState();
      for (const index of [0, 1] as const) {
        const message = betAnnouncementFor(previous.panels[index], next.panels[index]);
        if (message !== null) setAnnouncement(message);
      }
      previous = next;
    });
  }, [client]);
  return announcement;
}

/** APOSTA 1 and APOSTA 2 (slot `bets` of the Hud). */
export function PipaBets({ client, round }: PipaBetsProps): JSX.Element {
  const state = useBetsState(client.store);
  const [drafts, setDrafts] = useState<readonly [BetDraft, BetDraft]>([initialDraft, initialDraft]);
  const announcement = useBetAnnouncement(client);

  const setDraft = (panel: PanelNumber, draft: BetDraft): void => {
    setDrafts((current) => (panel === 1 ? [draft, current[1]] : [current[0], draft]));
    if (state.panels[panel - 1]?.error !== null) {
      client.store.dispatch({ type: "errorDismissed", panel });
    }
  };

  const placeBet = (panel: PanelNumber): void => {
    const draft = drafts[panel - 1] ?? initialDraft;
    const amount = parseMoneyInput(draft.amount);
    if (amount === null) return;
    const autoCashOut = draft.autoCashOutEnabled
      ? parseMultiplierInput(draft.autoCashOutTarget)
      : null;
    // A refusal is shown by the panel (its error); nothing else to do here.
    client.placeBet(panel, { amount, autoCashOut }).catch(() => undefined);
  };

  return (
    <div className="flex flex-col gap-3">
      <p className="sr-only" aria-live="polite" aria-atomic="true">
        {announcement}
      </p>
      {panelNumbers.map((panel) => (
        <BetPanel
          key={panel}
          panel={state.panels[panel - 1] ?? state.panels[0]}
          draft={drafts[panel - 1] ?? initialDraft}
          limits={state.limits}
          phase={round.phase}
          multiplier={round.multiplier}
          onDraftChange={(draft) => {
            setDraft(panel, draft);
          }}
          onPlaceBet={() => {
            placeBet(panel);
          }}
          onCancel={() => {
            client.cancelBet(panel).catch(() => undefined);
          }}
          onCashOut={() => {
            client.cashOut(panel).catch(() => undefined);
          }}
        />
      ))}
    </div>
  );
}

export interface PipaBalanceProps {
  readonly client: BetsClient;
}

/** "R$ 2.990,00 · DEMO" and "Recarregar demo" (slot `balance` of the Hud). */
export function PipaBalance({ client }: PipaBalanceProps): JSX.Element {
  const state = useBetsState(client.store);
  const [recharging, setRecharging] = useState(false);
  // The last refusal of the demo recharge, shown next to the balance until the next attempt (UT-10).
  const [rechargeError, setRechargeError] = useState<BetErrorCode | null>(null);
  return (
    <BalanceDisplay
      balance={state.balance}
      limits={state.limits}
      recharging={recharging}
      rechargeError={rechargeError}
      onRecharge={() => {
        setRecharging(true);
        setRechargeError(null);
        client
          .rechargeDemo()
          .catch((error: unknown) => {
            setRechargeError(error instanceof BetError ? error.code : "UNKNOWN");
          })
          .finally(() => {
            setRecharging(false);
          });
      }}
    />
  );
}
