// Scene part of the test hook (SPEC-0011 §6, ADR-0009): `window.__APOGEU_TEST__.scene` — freeze(t, state),
// effectsLog() and frameTimes() — installed next to the SPEC-0010 hook, only in test builds.

import type { PipaAnimator, SceneTestHook } from "@apogeu/scene-kit";

import { isTestBuild } from "../test-hook";

declare module "../test-hook" {
  interface ApogeuTestHook {
    scene?: SceneTestHook;
  }
}

/** Waits for the next rendered frame (the renderer's frame of this tick has run). */
const nextFrame = (): Promise<void> =>
  new Promise((resolve) => {
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        resolve();
      });
    });
  });

/**
 * Scenes mounted now, the newest last. React StrictMode mounts the scene twice and the two mounts may resolve in any
 * order, so the hook always points at the newest scene still mounted.
 */
const mounted: SceneTestHook[] = [];

/** Installs the scene hook in test builds; returns the function that removes it. */
export function installSceneTestHook(
  animator: PipaAnimator,
  frameTimes: readonly number[],
): () => void {
  const hook = window.__APOGEU_TEST__;
  if (!isTestBuild || hook === undefined) return () => undefined;
  const scene: SceneTestHook = {
    async freeze(atMs, state) {
      animator.freeze(atMs, state);
      await nextFrame();
    },
    effectsLog: () => animator.effectsLog(),
    frameTimes: () => frameTimes,
  };
  mounted.push(scene);
  hook.scene = scene;
  return () => {
    const index = mounted.indexOf(scene);
    if (index >= 0) mounted.splice(index, 1);
    const newest = mounted.at(-1);
    if (newest === undefined) delete hook.scene;
    else hook.scene = newest;
  };
}
