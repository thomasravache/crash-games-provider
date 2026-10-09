// Hidden tab (SPEC-0011 §5, ADR-0021): the scene pauses while the document is hidden and, when it is visible again,
// goes straight to the current state without replaying the animations it missed.

import { notImplemented } from "./not-implemented";

export interface VisibilitySource {
  readonly visibilityState: DocumentVisibilityState;
  addEventListener(type: "visibilitychange", listener: () => void): void;
  removeEventListener(type: "visibilitychange", listener: () => void): void;
}

export interface PausableTicker {
  start(): void;
  stop(): void;
}

export interface Pausable {
  pause(): void;
  resume(): void;
}

/** Stops the ticker and pauses the scene while `source` is hidden; returns the function that stops watching. */
export function pauseWhenHidden(
  source: VisibilitySource,
  ticker: PausableTicker,
  scene: Pausable,
): () => void {
  return notImplemented(source, ticker, scene);
}
