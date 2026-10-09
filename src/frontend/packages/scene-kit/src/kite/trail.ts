// Trail of SPEC-0011 §2: the glowing trail of the flight curve, with a bounded number of points.

import type { Vec2 } from "./kite-rig";
import { notImplemented } from "./not-implemented";

export interface Trail {
  push(point: Vec2): void;
  points(): readonly Vec2[];
  clear(): void;
}

export function createTrail(capacity: number): Trail {
  return notImplemented(capacity);
}
