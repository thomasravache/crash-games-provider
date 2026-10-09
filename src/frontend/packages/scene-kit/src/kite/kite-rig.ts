// KiteRig of SPEC-0011 §4: kite line and tail (rabiola) as Verlet ropes integrated with a fixed step, so the physics
// never depends on the frame rate (ADR-0021 principle 4).

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
  /** Moves the pinned first point. */
  setAnchor(anchor: Vec2): void;
  /** Pins the last point (the kite holding the line) or frees it (`null`, the line was cut). */
  setEnd(end: Vec2 | null): void;
  /** Changes the rest length of each segment (the line pays out as the kite climbs). */
  setSegmentLength(segmentLength: number): void;
  /** Pushes every free point by `velocity` (px/s) — the recoil of a cut line. */
  impulse(velocity: Vec2, fromIndex?: number): void;
  /** Lays the rope straight from the anchor towards `direction`, at rest. */
  lay(direction: Vec2): void;
}

/** Gravity of the rig, px/s². */
const defaultGravity: Vec2 = { x: 0, y: 420 };
/** Velocity kept per step (air drag). */
const damping = 0.992;
/** Relaxation passes of the length constraints per step. */
const iterations = 24;
/** Frame time integrated at most per advance (a long pause never makes the rope run hundreds of steps). */
const maxAdvanceSeconds = 0.25;
const epsilon = 1e-9;

export function createVerletRope(options: VerletRopeOptions): VerletRope {
  const count = Math.max(1, Math.floor(options.segments)) + 1;
  const stepSeconds = options.stepSeconds;
  const gravity = options.gravity ?? defaultGravity;
  const xs = new Float64Array(count);
  const ys = new Float64Array(count);
  const oldXs = new Float64Array(count);
  const oldYs = new Float64Array(count);
  let anchor = options.anchor;
  let end: Vec2 | null = null;
  let segmentLength = options.segmentLength;
  let steps = 0;
  let pending = 0;

  const lay = (direction: Vec2): void => {
    const norm = Math.hypot(direction.x, direction.y) || 1;
    for (let index = 0; index < count; index += 1) {
      xs[index] = anchor.x + (direction.x / norm) * segmentLength * index;
      ys[index] = anchor.y + (direction.y / norm) * segmentLength * index;
      oldXs[index] = xs[index] ?? 0;
      oldYs[index] = ys[index] ?? 0;
    }
  };
  lay({ x: 0, y: 1 });

  const pin = (): void => {
    xs[0] = anchor.x;
    ys[0] = anchor.y;
    oldXs[0] = anchor.x;
    oldYs[0] = anchor.y;
    if (end !== null) {
      const last = count - 1;
      xs[last] = end.x;
      ys[last] = end.y;
      oldXs[last] = end.x;
      oldYs[last] = end.y;
    }
  };

  const satisfy = (): void => {
    const lastFree = end === null;
    for (let pass = 0; pass < iterations; pass += 1) {
      for (let index = 0; index < count - 1; index += 1) {
        const ax = xs[index] ?? 0;
        const ay = ys[index] ?? 0;
        const bx = xs[index + 1] ?? 0;
        const by = ys[index + 1] ?? 0;
        const dx = bx - ax;
        const dy = by - ay;
        const distance = Math.hypot(dx, dy);
        if (distance < epsilon) continue;
        const error = (distance - segmentLength) / distance;
        const aPinned = index === 0;
        const bPinned = index + 1 === count - 1 && !lastFree;
        if (aPinned && bPinned) continue;
        const aShare = aPinned ? 0 : bPinned ? 1 : 0.5;
        const bShare = 1 - aShare;
        xs[index] = ax + dx * error * aShare;
        ys[index] = ay + dy * error * aShare;
        xs[index + 1] = bx - dx * error * bShare;
        ys[index + 1] = by - dy * error * bShare;
      }
    }
  };

  const step = (wind: WindField): void => {
    const force = wind(steps * stepSeconds);
    const ax = (gravity.x + force.x) * stepSeconds * stepSeconds;
    const ay = (gravity.y + force.y) * stepSeconds * stepSeconds;
    for (let index = 1; index < count; index += 1) {
      const x = xs[index] ?? 0;
      const y = ys[index] ?? 0;
      const vx = (x - (oldXs[index] ?? x)) * damping;
      const vy = (y - (oldYs[index] ?? y)) * damping;
      oldXs[index] = x;
      oldYs[index] = y;
      xs[index] = x + vx + ax;
      ys[index] = y + vy + ay;
    }
    pin();
    satisfy();
    pin();
    steps += 1;
  };

  return {
    step,
    advance(dtSeconds, wind) {
      pending = Math.min(maxAdvanceSeconds, pending + Math.max(0, dtSeconds));
      let done = 0;
      while (pending >= stepSeconds - epsilon) {
        step(wind);
        pending -= stepSeconds;
        done += 1;
      }
      return done;
    },
    points() {
      const points: Vec2[] = [];
      for (let index = 0; index < count; index += 1)
        points.push({ x: xs[index] ?? 0, y: ys[index] ?? 0 });
      return points;
    },
    length() {
      let total = 0;
      for (let index = 0; index < count - 1; index += 1) {
        total += Math.hypot(
          (xs[index + 1] ?? 0) - (xs[index] ?? 0),
          (ys[index + 1] ?? 0) - (ys[index] ?? 0),
        );
      }
      return total;
    },
    time() {
      return steps * stepSeconds;
    },
    setAnchor(next) {
      anchor = next;
      pin();
    },
    setEnd(next) {
      end = next;
      pin();
    },
    setSegmentLength(next) {
      segmentLength = Math.max(epsilon, next);
    },
    impulse(velocity, fromIndex = 1) {
      for (let index = Math.max(1, fromIndex); index < count; index += 1) {
        if (index === count - 1 && end !== null) continue;
        oldXs[index] = (xs[index] ?? 0) - velocity.x * stepSeconds;
        oldYs[index] = (ys[index] ?? 0) - velocity.y * stepSeconds;
      }
    },
    lay,
  };
}
