// Sound control of the game header (SPEC-0018 §6, ADR-0020): the "Som" button (aria-pressed = sound on) and the
// volume control, operable by keyboard and screen reader. The first gesture on any game control calls unlock().

import type { JSX } from "react";
import { notImplemented } from "./not-implemented";

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

export function SoundToggle(props: SoundToggleProps): JSX.Element {
  return notImplemented(props);
}
