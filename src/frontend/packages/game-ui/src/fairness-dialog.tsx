// "Comprovadamente Justo" window (SPEC-0010 §6): the player picks a finished round, the browser fetches its
// verification data and checks it with verifyRound (game-sdk), showing "Verificado" and the recomputed crash point.

import {
  computeCrashPoint,
  verifyRound,
  type RoundVerificationData,
  type VerificationResult,
} from "@apogeu/game-sdk/fairness";
import type { HistoryEntry } from "@apogeu/game-sdk/state";
import { useEffect, useId, useRef, useState, type JSX } from "react";

import { formatMultiplier } from "./format";
import { hudTestIds, hudText } from "./hud-text";

export interface FairnessDialogProps {
  readonly history: readonly HistoryEntry[];
  /** GET /api/v1/rounds/{roundId}/verification. */
  readonly loadVerification: (roundId: string) => Promise<RoundVerificationData>;
  readonly onClose: () => void;
}

type Check =
  | { readonly status: "idle" }
  | { readonly status: "verifying"; readonly round: HistoryEntry }
  | {
      readonly status: "done";
      readonly round: HistoryEntry;
      readonly data: RoundVerificationData;
      readonly result: VerificationResult;
      readonly recomputed: string;
    }
  | { readonly status: "unavailable"; readonly round: HistoryEntry };

const resultText: Record<Exclude<VerificationResult, "valid">, string> = {
  "chain-mismatch": hudText.chainMismatch,
  "crash-mismatch": hudText.crashMismatch,
};

function Detail({ label, value }: { readonly label: string; readonly value: string }): JSX.Element {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-hud-muted">{label}</dt>
      <dd className="font-mono text-xs break-all">{value}</dd>
    </div>
  );
}

export function FairnessDialog({
  history,
  loadVerification,
  onClose,
}: FairnessDialogProps): JSX.Element {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const [check, setCheck] = useState<Check>({ status: "idle" });
  const request = useRef(0);

  useEffect(() => {
    const element = dialog.current;
    if (element !== null && !element.open) element.showModal();
    return () => {
      if (element?.open === true) element.close();
    };
  }, []);

  const choose = (round: HistoryEntry): void => {
    request.current += 1;
    const mine = request.current;
    setCheck({ status: "verifying", round });
    void (async () => {
      try {
        const data = await loadVerification(round.roundId);
        const [result, recomputed] = await Promise.all([
          verifyRound(data),
          computeCrashPoint(data),
        ]);
        if (mine === request.current) setCheck({ status: "done", round, data, result, recomputed });
      } catch {
        if (mine === request.current) setCheck({ status: "unavailable", round });
      }
    })();
  };

  return (
    <dialog
      ref={dialog}
      aria-labelledby={titleId}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      className="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-2xl border border-hud-border bg-hud-surface p-0 text-hud-text backdrop:bg-black/60"
    >
      <div className="flex flex-col gap-4 p-5">
        <div className="flex items-start gap-3">
          <h2 id={titleId} className="font-hud-display text-xl font-bold">
            {hudText.fairnessTitle}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="ml-auto min-h-11 rounded-lg px-3 text-sm text-hud-muted hover:text-hud-text focus-visible:outline-2 focus-visible:outline-hud-win"
          >
            {hudText.close}
          </button>
        </div>
        <p className="text-sm text-hud-muted">{hudText.pickRound}</p>

        <ul className="flex max-h-40 flex-wrap gap-2 overflow-y-auto">
          {history.map((round) => {
            const chosen = check.status !== "idle" && check.round.roundId === round.roundId;
            return (
              <li key={round.roundId}>
                <button
                  type="button"
                  aria-pressed={chosen}
                  onClick={() => {
                    choose(round);
                  }}
                  className={`min-h-11 rounded-lg border px-3 text-sm tabular-nums focus-visible:outline-2 focus-visible:outline-hud-win ${chosen ? "border-hud-win bg-hud-win/10" : "border-hud-border bg-hud-surface-raised"}`}
                >
                  {`Rodada ${String(round.roundNumber)} · ${formatMultiplier(round.crashPoint)}`}
                </button>
              </li>
            );
          })}
        </ul>

        <section aria-live="polite" className="flex flex-col gap-3">
          {check.status === "verifying" ? <p className="text-sm">{hudText.verifying}</p> : null}
          {check.status === "unavailable" ? (
            <p className="text-sm text-hud-crashed">{hudText.verificationUnavailable}</p>
          ) : null}
          {check.status === "done" ? (
            <>
              <div className="flex items-baseline gap-3">
                {check.result === "valid" ? (
                  <p className="font-hud-display text-lg font-bold text-hud-win">
                    {hudText.verified}
                  </p>
                ) : (
                  <p className="text-sm font-semibold text-hud-crashed">
                    {resultText[check.result]}
                  </p>
                )}
                <p className="ml-auto text-sm">
                  <span className="text-hud-muted">Crash recalculado: </span>
                  <span
                    data-testid={hudTestIds.fairnessCrashPoint}
                    className="font-hud-display font-bold tabular-nums"
                  >
                    {formatMultiplier(check.recomputed)}
                  </span>
                </p>
              </div>
              <dl className="grid gap-2">
                <Detail label="Semente revelada" value={check.data.seed} />
                <Detail label="Hash da rodada anterior" value={check.data.previousSeed} />
                <Detail label="Sal da cadeia" value={check.data.salt} />
                <Detail
                  label="RTP / limite / crash informado"
                  value={`${check.data.rtp} / ${check.data.cap} / ${check.data.crashPoint}`}
                />
              </dl>
            </>
          ) : null}
        </section>
      </div>
    </dialog>
  );
}
