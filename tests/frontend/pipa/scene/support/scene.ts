// Helpers of the SPEC-0011 unit tests: a seeded PipaAnimator and the scripted round it is driven through (a 60 fps
// "ticker" made of advance() calls). Every random choice of the scene comes from the seeded generator.
import { pipaSkyTheme } from "@apogeu/game-pipa/scene/sky-theme";
import {
  createPipaAnimator,
  type MotionProfile,
  type PipaAnimator,
  type SceneFrame,
} from "@apogeu/scene-kit";

/** One frame at 60 fps, ms. */
export const frameMs = 1000 / 60;

/** Mulberry32: small deterministic generator for the scene's randomness. */
export function seededRandom(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function newAnimator(
  profile: MotionProfile,
  devicePixelRatio = 1,
): PipaAnimator {
  return createPipaAnimator({
    profile,
    sky: pipaSkyTheme,
    devicePixelRatio,
    random: seededRandom(11),
  });
}

/** Advances `durationMs` in 60 fps frames; returns every frame drawn. */
export function run(animator: PipaAnimator, durationMs: number, stepMs = frameMs): SceneFrame[] {
  const frames: SceneFrame[] = [];
  for (let elapsed = 0; elapsed < durationMs; elapsed += stepMs) {
    frames.push(animator.advance(stepMs));
  }
  return frames;
}

/**
 * Flight from the current multiplier up to `target`, growing 2% per frame as the interpolated multiplier of the store
 * does (one setMultiplier and one frame at a time); returns every frame drawn.
 */
export function flyTo(animator: PipaAnimator, from: number, target: number): SceneFrame[] {
  const frames: SceneFrame[] = [];
  for (let value = from; value < target; value = Math.min(target, value * 1.02)) {
    animator.setMultiplier(value);
    frames.push(animator.advance(frameMs));
  }
  animator.setMultiplier(target);
  frames.push(animator.advance(frameMs));
  return frames;
}

/** A whole round: betting, takeoff, flight up to `crashPoint`, crash and its 2 s of animation, next betting. */
export function playRound(animator: PipaAnimator, crashPoint: number): SceneFrame[] {
  animator.prepare();
  const frames = run(animator, 1_000);
  animator.takeoff(0);
  frames.push(...run(animator, 700));
  frames.push(...flyTo(animator, 1, crashPoint));
  animator.crash(crashPoint);
  frames.push(...run(animator, 2_000));
  animator.prepare();
  frames.push(...run(animator, 1_000));
  return frames;
}
