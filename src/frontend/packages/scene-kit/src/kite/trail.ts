// Trail of SPEC-0011 §2: the glowing trail of the flight curve, with a bounded number of points.

import type { Vec2 } from "./kite-rig";

export interface Trail {
  push(point: Vec2): void;
  points(): readonly Vec2[];
  clear(): void;
}

export function createTrail(capacity: number): Trail {
  const size = Math.max(1, Math.floor(capacity));
  let items: Vec2[] = [];
  return {
    push(point) {
      items.push(point);
      if (items.length > size) items = items.slice(items.length - size);
    },
    points() {
      return items;
    },
    clear() {
      items = [];
    },
  };
}
