// HUD from the Stitch screen "Pipa Crash - Jogo da Pipa" (SPEC-0010 §6): history, multiplier, phase banner, DEMO
// badge, fixed demo balance, disabled bet panel ("Apostas em breve"), "Comprovadamente Justo" entry and an aria-live
// region announcing only the phase changes (ADR-0020).

import type { RoundState } from "@apogeu/game-sdk/state";
import type { JSX, ReactNode } from "react";

import { formatMultiplier, secondsLeft } from "./format";
import { HistoryStrip } from "./history-strip";
import { hudAnnouncement, hudTestIds, hudText } from "./hud-text";
import { useNow } from "./use-round-state";

export interface HudProps {
  readonly state: RoundState;
  /** Player nickname (the only personal data shown). */
  readonly nickname: string;
  /** Fixed demo balance, formatted decimal string. */
  readonly demoBalance: string;
  readonly onOpenFairness: () => void;
  /** The game scene (decorative canvas), drawn behind the multiplier. */
  readonly scene?: ReactNode;
  /** Windows opened from the HUD (for example "Comprovadamente Justo"), kept inside the main landmark. */
  readonly children?: ReactNode;
}

function announcement(state: RoundState): string {
  switch (state.phase) {
    case "betting":
      return hudAnnouncement.betting(state.roundNumber);
    case "flying":
      return hudAnnouncement.flying;
    case "crashed":
      return hudAnnouncement.crashed(formatMultiplier(state.crashPoint ?? state.multiplier));
    case "annulled":
      return hudAnnouncement.annulled;
    case "idle":
      return "";
  }
}

function Readout({ state }: { readonly state: RoundState }): JSX.Element {
  const now = useNow(state.phase === "betting");

  switch (state.phase) {
    case "betting":
      return (
        <>
          <p className="font-hud-display text-sm font-semibold tracking-wider text-hud-betting">
            {hudText.betting}
          </p>
          <p
            data-testid={hudTestIds.countdown}
            className="font-hud-display text-5xl font-bold tabular-nums text-hud-text md:text-6xl"
          >
            {`${String(secondsLeft(state.bettingEndsAt, state.serverOffsetMs, now))}s`}
          </p>
        </>
      );
    case "flying":
      return (
        <>
          <p
            data-testid={hudTestIds.multiplier}
            className="font-hud-display text-5xl font-bold tabular-nums text-hud-text md:text-7xl"
          >
            {formatMultiplier(state.multiplier)}
          </p>
          <p className="font-hud-display text-sm font-semibold tracking-wider text-hud-flying">
            {hudText.flying}
          </p>
        </>
      );
    case "crashed":
      return (
        <>
          <p
            data-testid={hudTestIds.multiplier}
            className="font-hud-display text-5xl font-bold tabular-nums text-hud-crashed md:text-7xl"
          >
            {formatMultiplier(state.crashPoint ?? state.multiplier)}
          </p>
          <p className="font-hud-display text-sm font-semibold tracking-wider text-hud-crashed">
            {hudText.crashed}
          </p>
        </>
      );
    case "annulled":
      return (
        <p className="font-hud-display text-lg font-semibold text-hud-text">{hudText.annulled}</p>
      );
    case "idle":
      return <p className="font-hud-body text-sm text-hud-muted">{hudText.waiting}</p>;
  }
}

export function Hud({
  state,
  nickname,
  demoBalance,
  onOpenFairness,
  scene,
  children,
}: HudProps): JSX.Element {
  return (
    <div className="flex min-h-dvh flex-col bg-hud-canvas font-hud-body text-hud-text">
      <header className="flex items-center gap-3 border-b border-hud-border bg-hud-surface px-4 py-2">
        <h1 className="font-hud-display text-lg font-bold tracking-tight">{hudText.brand}</h1>
        <span className="rounded-full bg-hud-accent px-2 py-0.5 font-hud-display text-xs font-bold tracking-widest text-hud-canvas">
          {hudText.demoBadge}
        </span>
        {state.connection === "reconnecting" ? (
          <span
            role="status"
            className="rounded-full border border-hud-flying/40 px-2 py-0.5 text-xs text-hud-flying"
          >
            {hudText.reconnecting}
          </span>
        ) : null}
        <div className="ml-auto text-right">
          <p className="max-w-40 truncate text-xs text-hud-muted">{nickname}</p>
          <p className="text-sm font-semibold tabular-nums">
            <span className="sr-only">{`${hudText.balance}: `}</span>
            <span data-testid={hudTestIds.balance}>{demoBalance}</span>
          </p>
        </div>
      </header>

      <main className="flex flex-1 flex-col md:grid md:grid-cols-12">
        <p className="sr-only" aria-live="polite" aria-atomic="true">
          {announcement(state)}
        </p>

        <section aria-label="Rodada" className="flex min-h-[45dvh] flex-col md:col-span-8">
          <HistoryStrip history={state.history} />
          <div className="relative flex-1 overflow-hidden">
            <div className="absolute inset-0" aria-hidden="true">
              {scene}
            </div>
            <div className="pointer-events-none absolute inset-0 flex items-center justify-center p-4">
              <div className="flex min-w-48 flex-col items-center gap-1 rounded-2xl border border-hud-border bg-hud-surface/85 px-6 py-4 text-center backdrop-blur">
                <Readout state={state} />
              </div>
            </div>
          </div>
        </section>

        <section
          aria-label="Painel de aposta"
          className="flex flex-col gap-3 border-t border-hud-border bg-hud-surface p-4 md:col-span-4 md:border-t-0 md:border-l"
        >
          <button
            type="button"
            disabled
            className="min-h-12 w-full cursor-not-allowed rounded-xl bg-hud-surface-raised font-hud-display text-base font-bold tracking-wide text-hud-muted"
          >
            {hudText.betDisabled}
          </button>
          <button
            type="button"
            onClick={onOpenFairness}
            className="min-h-12 w-full rounded-xl border border-hud-win/50 font-hud-display text-sm font-semibold text-hud-win hover:bg-hud-win/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-hud-win"
          >
            {hudText.fairnessTitle}
          </button>
        </section>
        {children}
      </main>
    </div>
  );
}
