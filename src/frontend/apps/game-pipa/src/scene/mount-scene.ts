// Scene of the Pipa game (SPEC-0011): the PixiJS Pipa scene of scene-kit with the Pipa sky, driven by the round state
// through GameAnimator (ADR-0004). Loaded on demand, so the renderer stays out of the initial bundle (ADR-0019).
// Imports only the state of game-sdk, never the transport.

import type { RoundStateSource } from "@apogeu/game-sdk/state";
import { bindGameAnimator } from "@apogeu/scene-kit";
import { createPipaScene } from "@apogeu/scene-kit/pixi";

import { isTestBuild } from "../test-hook";
import { installSceneTestHook } from "./scene-test-hook";
import { pipaSkyGradients, pipaSkyTheme } from "./sky-theme";

/** Mounts the scene in `host`; returns the function that unmounts it. */
export async function mountPipaScene(
  host: HTMLElement,
  source: RoundStateSource,
): Promise<() => void> {
  // Frame times are only kept for the test hook (SPEC-0011 E2E-03).
  const frameTimes: number[] = [];
  const scene = await createPipaScene(host, {
    sky: pipaSkyTheme,
    gradients: pipaSkyGradients,
    recordEffects: isTestBuild,
    ...(isTestBuild ? { onFrame: (workMs: number) => frameTimes.push(workMs) } : {}),
  });
  const uninstall = installSceneTestHook(scene.animator, frameTimes);
  const unbind = bindGameAnimator(source, scene.animator);
  return () => {
    unbind();
    uninstall();
    scene.destroy();
  };
}
