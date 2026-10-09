// Scene of the Pipa game: the PixiJS kite of scene-kit with the Pipa sky, driven by the round state through
// GameAnimator (ADR-0004). Loaded on demand, so the renderer stays out of the initial bundle (ADR-0019). Imports
// only the state of game-sdk, never the transport.

import type { RoundStateSource } from "@apogeu/game-sdk/state";
import { bindGameAnimator } from "@apogeu/scene-kit";
import { createKiteScene } from "@apogeu/scene-kit/pixi";

import { pipaSkyTheme } from "./sky-theme";

/** Mounts the scene in `host`; returns the function that unmounts it. */
export async function mountPipaScene(
  host: HTMLElement,
  source: RoundStateSource,
): Promise<() => void> {
  const scene = await createKiteScene(host, { sky: pipaSkyTheme });
  const unbind = bindGameAnimator(source, scene.animator);
  return () => {
    unbind();
    scene.destroy();
  };
}
