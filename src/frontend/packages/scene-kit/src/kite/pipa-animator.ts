// PipaAnimator of SPEC-0011 §4: the GameAnimator of the Pipa scene (contract of SPEC-0010@1, unchanged), composed of
// Sky, Camera, CloudLayers, KiteRig, Trail, Effects and Timeline. Pure: the renderer advances it on each tick and
// draws the SceneFrame it returns, so every visual parameter is testable without rendering.

import type { GameAnimator } from "../game-animator";
import type { SkyTheme } from "../sky";
import type { CloudLayerState } from "./cloud-layers";
import type { EffectEntry } from "./effects";
import type { Vec2 } from "./kite-rig";
import type { MotionProfile } from "./motion-profile";
import { notImplemented } from "./not-implemented";
import type { SkyState } from "./sky-ramp";
import type { Pausable } from "./visibility";

/** Phase of the scene's script (SPEC-0011 §2). */
export type ScenePhase = "betting" | "takeoff" | "flying" | "crashed" | "restart";

/** Visual parameters of the scene at an instant. */
export interface SceneFrame {
  /** Scene clock, ms (advances only while running). */
  readonly timeMs: number;
  readonly phase: ScenePhase;
  /** Multiplier the scene shows (the crash point once crashed). */
  readonly multiplier: number;
  readonly sky: SkyState;
  readonly stars: number;
  readonly moon: boolean;
  /** Camera altitude, screens. */
  readonly cameraAltitude: number;
  readonly wind: number;
  readonly clouds: readonly CloudLayerState[];
  /** Screen shake offset, px. */
  readonly shake: Vec2;
  /** Opacity of the red flash. */
  readonly flashOpacity: number;
  /** Live particles. */
  readonly particles: number;
  /** Active filters (glow, bloom, ...). */
  readonly activeFilters: number;
  /** Renderer resolution (pixel density). */
  readonly resolution: number;
}

export interface PipaAnimatorOptions {
  readonly profile: MotionProfile;
  readonly sky: SkyTheme;
  /** Device pixel ratio of the display (default 1). */
  readonly devicePixelRatio?: number;
  /** Source of randomness for particles and gusts (default Math.random); tests pass a seeded one. */
  readonly random?: () => number;
}

export interface PipaAnimator extends GameAnimator, Pausable {
  readonly profile: MotionProfile;
  /** Advances the scene clock by `dtMs` (ignored while paused) and returns the frame to draw. */
  advance(dtMs: number): SceneFrame;
  /** The current frame, without advancing. */
  frame(): SceneFrame;
  /** Effects started so far, in order. */
  effectsLog(): readonly EffectEntry[];
}

export function createPipaAnimator(options: PipaAnimatorOptions): PipaAnimator {
  return notImplemented(options);
}
