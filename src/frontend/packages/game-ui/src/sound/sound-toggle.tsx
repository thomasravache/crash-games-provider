// Sound control of the game header (SPEC-0018 §6, ADR-0020): the "Som" button (aria-pressed = sound on) and the
// volume control, operable by keyboard and screen reader. The first gesture on any game control calls unlock().

import type { JSX } from "react";

export const soundText = {
  toggle: "Som",
  volume: "Volume",
} as const;

export interface SoundToggleProps {
  readonly muted: boolean;
  /** Master volume in [0, 1]. */
  readonly volume: number;
  readonly onMutedChange: (muted: boolean) => void;
  readonly onVolumeChange: (volume: number) => void;
}

/** Speaker icon, on or crossed out (decorative: the state is the button's aria-pressed). */
function SpeakerIcon({ on }: { readonly on: boolean }): JSX.Element {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="h-5 w-5"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M11 5 6 9H3v6h3l5 4z" fill="currentColor" />
      {on ? (
        <path d="M15.5 8.5a5 5 0 0 1 0 7M18.5 5.5a9 9 0 0 1 0 13" />
      ) : (
        <path d="m16 9 6 6M22 9l-6 6" />
      )}
    </svg>
  );
}

export function SoundToggle({
  muted,
  volume,
  onMutedChange,
  onVolumeChange,
}: SoundToggleProps): JSX.Element {
  const on = !muted;
  return (
    <div className="flex items-center gap-2">
      <button
        type="button"
        aria-pressed={on}
        onClick={() => {
          onMutedChange(on);
        }}
        className={`flex min-h-10 min-w-10 items-center justify-center rounded-full border border-hud-border hover:bg-hud-surface-raised focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-hud-win ${on ? "text-hud-text" : "text-hud-muted"}`}
      >
        <SpeakerIcon on={on} />
        <span className="sr-only">{soundText.toggle}</span>
      </button>
      <input
        type="range"
        min={0}
        max={100}
        step={5}
        value={Math.round(volume * 100)}
        aria-label={soundText.volume}
        onChange={(event) => {
          onVolumeChange(Number(event.currentTarget.value) / 100);
        }}
        className="hidden w-20 accent-hud-win sm:block"
      />
    </div>
  );
}
