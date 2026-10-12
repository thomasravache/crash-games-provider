// One bet panel of the Stitch screen "Pipa Crash - Jogo da Pipa" (SPEC-0026 §6): amount with − / + (step R$ 1,00),
// chips 1, 5, 10, 50 (they set the amount), automatic cash-out (switch + target ≥ 1,01x) and the APOSTAR (green) /
// CANCELAR / RETIRAR (orange) button, with PRÓXIMA while the bet waits for the next round. Each panel is an instance of
// this component; it only sends intents, the state comes from the server through the bets store.

import {
  formatMoney,
  parseMoneyInput,
  quickChips,
  stepAmount,
  type BetDraft,
  type BetLimits,
  type Money,
  type PanelState,
  type RoundPhase,
} from "@apogeu/game-sdk/state";
import { useId, type JSX } from "react";

import { betPanelView, type BetPanelAction } from "./bet-panel-view";
import { betPanelText, betTestIds } from "./bet-text";

export interface BetPanelProps {
  readonly panel: PanelState;
  /** What the player set (controlled: the app keeps one draft per panel). */
  readonly draft: BetDraft;
  readonly limits: BetLimits | null;
  readonly phase: RoundPhase;
  /** Current multiplier of the round, for the RETIRAR preview. */
  readonly multiplier: number;
  readonly onDraftChange: (draft: BetDraft) => void;
  readonly onPlaceBet: () => void;
  readonly onCancel: () => void;
  readonly onCashOut: () => void;
}

/** "10.00" → "10,00": the pt-BR text the amount field holds. */
export const amountInputText = (value: Money): string => formatMoney(value).replace(/^R\$\s/u, "");

/** "10.00" → "R$ 10" for the chips. */
const chipText = (value: Money): string => formatMoney(value).replace(/,00$/u, "");

const actionStyles: Record<BetPanelAction, string> = {
  bet: "bg-hud-win text-hud-canvas hover:brightness-110",
  cancel: "border border-hud-crashed bg-hud-surface text-hud-text hover:bg-hud-crashed/20",
  cashOut: "bg-hud-accent text-hud-canvas hover:brightness-110",
  none: "bg-hud-surface text-hud-text",
};

const focusRing =
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-hud-win";

const smallButton = `min-h-10 min-w-10 rounded-lg border border-hud-border font-hud-display text-sm font-semibold text-hud-text hover:bg-hud-surface-raised disabled:cursor-not-allowed disabled:opacity-50 ${focusRing}`;

export function BetPanel({
  panel,
  draft,
  limits,
  phase,
  multiplier,
  onDraftChange,
  onPlaceBet,
  onCancel,
  onCashOut,
}: BetPanelProps): JSX.Element {
  const view = betPanelView({ panel, draft, limits, phase, multiplier });
  const id = useId();
  const titleId = `${id}-title`;
  const reasonId = `${id}-reason`;
  const autoId = `${id}-auto`;
  const targetId = `${id}-target`;
  const amountId = `${id}-amount`;
  const n = panel.panel;

  const step = (direction: -1 | 1): void => {
    const current = parseMoneyInput(draft.amount) ?? limits?.minBet ?? ("1.00" as Money);
    onDraftChange({ ...draft, amount: amountInputText(stepAmount(current, direction, limits)) });
  };

  const act = (): void => {
    switch (view.action) {
      case "bet":
        onPlaceBet();
        return;
      case "cancel":
        onCancel();
        return;
      case "cashOut":
        onCashOut();
        return;
      case "none":
        return;
    }
  };

  return (
    <section
      aria-labelledby={titleId}
      data-testid={betTestIds.panel(n)}
      className="flex flex-col gap-3 rounded-2xl border border-hud-border bg-hud-canvas/40 p-3"
    >
      <div className="flex items-center gap-2">
        <h2
          id={titleId}
          className="font-hud-display text-sm font-bold tracking-wider text-hud-muted"
        >
          {view.title}
        </h2>
        {view.badge === null ? null : (
          <span className="rounded-full border border-hud-betting px-2 py-0.5 font-hud-display text-xs font-bold tracking-widest text-hud-betting">
            {view.badge}
          </span>
        )}
      </div>

      <div className="flex items-center gap-2">
        <button
          type="button"
          aria-label={betPanelText.decrease}
          disabled={!view.editable}
          onClick={() => {
            step(-1);
          }}
          className={smallButton}
        >
          −
        </button>
        <label htmlFor={amountId} className="sr-only">
          {betPanelText.amount}
        </label>
        <input
          id={amountId}
          type="text"
          inputMode="decimal"
          autoComplete="off"
          data-testid={betTestIds.amount(n)}
          value={draft.amount}
          disabled={!view.editable}
          onChange={(event) => {
            onDraftChange({ ...draft, amount: event.currentTarget.value });
          }}
          className="min-h-10 w-full min-w-0 flex-1 rounded-lg border border-hud-border bg-hud-canvas px-3 text-center font-hud-display text-base font-semibold tabular-nums text-hud-text disabled:opacity-60"
        />
        <button
          type="button"
          aria-label={betPanelText.increase}
          disabled={!view.editable}
          onClick={() => {
            step(1);
          }}
          className={smallButton}
        >
          +
        </button>
      </div>

      <div className="grid grid-cols-4 gap-2">
        {quickChips.map((chip) => (
          <button
            key={chip}
            type="button"
            aria-label={betPanelText.chip(formatMoney(chip))}
            disabled={!view.editable}
            onClick={() => {
              onDraftChange({ ...draft, amount: amountInputText(chip) });
            }}
            className={smallButton}
          >
            {chipText(chip)}
          </button>
        ))}
      </div>

      <div className="flex items-center gap-2">
        <input
          id={autoId}
          type="checkbox"
          checked={draft.autoCashOutEnabled}
          disabled={!view.editable}
          onChange={(event) => {
            onDraftChange({ ...draft, autoCashOutEnabled: event.currentTarget.checked });
          }}
          className={`h-5 w-5 accent-hud-win ${focusRing}`}
        />
        <label htmlFor={autoId} className="text-sm text-hud-text">
          {betPanelText.autoCashOut}
        </label>
        <label htmlFor={targetId} className="sr-only">
          {betPanelText.autoTarget}
        </label>
        <input
          id={targetId}
          type="text"
          inputMode="decimal"
          autoComplete="off"
          value={draft.autoCashOutTarget}
          disabled={!view.editable || !draft.autoCashOutEnabled}
          onChange={(event) => {
            onDraftChange({ ...draft, autoCashOutTarget: event.currentTarget.value });
          }}
          className="ml-auto min-h-10 w-20 rounded-lg border border-hud-border bg-hud-canvas px-2 text-center font-hud-display text-sm tabular-nums text-hud-text disabled:opacity-60"
        />
        <span aria-hidden="true" className="text-sm text-hud-muted">
          x
        </span>
      </div>

      <button
        type="button"
        data-testid={betTestIds.action(n)}
        disabled={view.disabled && !view.busy}
        aria-disabled={view.busy ? true : undefined}
        aria-describedby={view.disabledReason === null ? undefined : reasonId}
        onClick={() => {
          if (!view.disabled) act();
        }}
        className={`min-h-12 w-full rounded-xl font-hud-display text-base font-bold tracking-wide disabled:cursor-not-allowed disabled:opacity-60 aria-disabled:cursor-wait aria-disabled:opacity-60 ${focusRing} ${actionStyles[view.action]}`}
      >
        {view.label}
      </button>

      {view.disabledReason === null ? null : (
        <p
          id={reasonId}
          data-testid={betTestIds.disabledReason(n)}
          className="text-xs text-hud-muted"
        >
          {view.disabledReason}
        </p>
      )}
      {view.result === null ? null : (
        <p
          data-testid={betTestIds.result(n)}
          className={`font-hud-display text-sm font-bold ${panel.status === "cashed_out" ? "text-hud-win" : "text-hud-text"}`}
        >
          {view.result}
        </p>
      )}
      {view.note === null ? null : <p className="text-xs text-hud-muted">{view.note}</p>}
      {view.error === null ? null : (
        <p
          role="alert"
          data-testid={betTestIds.error(n)}
          className="rounded-lg border border-hud-crashed/60 px-2 py-1 text-xs text-hud-text"
        >
          {view.error}
        </p>
      )}
    </section>
  );
}
