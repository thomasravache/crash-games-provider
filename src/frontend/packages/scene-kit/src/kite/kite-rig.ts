// KiteRig of SPEC-0011 §4: kite line and tail (rabiola) as Verlet ropes integrated with a fixed step, so the physics
// never depends on the frame rate (ADR-0021 principle 4).

import { notImplemented } from "./not-implemented";

export interface Vec2 {
  readonly x: number;
  readonly y: number;
}

/** Wind force (px/s²) at the simulation time, in seconds. */
export type WindField = (timeSeconds: number) => Vec2;

export interface VerletRopeOptions {
  /** Pinned first point (the hand for the line, the kite for the tail). */
  readonly anchor: Vec2;
  readonly segments: number;
  /** Rest length of each segment, px. */
  readonly segmentLength: number;
  /** Fixed integration step, seconds (e.g. 1/120). */
  readonly stepSeconds: number;
  /** Gravity, px/s² (default: the rig's own). */
  readonly gravity?: Vec2;
}

export interface VerletRope {
  /** Integrates exactly one fixed step. */
  step(wind: WindField): void;
  /** Integrates the fixed steps that fit in `dtSeconds` of frame time (the remainder carries over); returns how many. */
  advance(dtSeconds: number, wind: WindField): number;
  /** The `segments + 1` points, anchor first. */
  points(): readonly Vec2[];
  /** Current length along the points, px. */
  length(): number;
  /** Simulation time, seconds. */
  time(): number;
}

export function createVerletRope(options: VerletRopeOptions): VerletRope {
  return notImplemented(options);
}
