// PipaAnimator of SPEC-0011 §4: the GameAnimator of the Pipa scene (contract of SPEC-0010@1, unchanged), composed of
// Sky, Camera, CloudLayers, KiteRig, Trail, Effects and Timeline. Pure: the renderer advances it on each tick and
// draws the SceneFrame it returns, so every visual parameter is testable without rendering.

import type { GameAnimator } from "../game-animator";
import type { SkyTheme } from "../sky";
import { cameraAltitude, windIntensity } from "./camera";
import { cloudLayers, type CloudLayerState } from "./cloud-layers";
import { maxActiveFilters, maxParticles, sceneResolution, type EffectEntry } from "./effects";
import { createVerletRope, type Vec2, type VerletRope, type WindField } from "./kite-rig";
import { createMilestoneTracker } from "./milestones";
import type { MotionProfile } from "./motion-profile";
import { crashScript, restingScript, transitionScript, type PhaseScript } from "./phase-script";
import type { SceneFreezeState } from "./scene-test-hook";
import { moonOpacity, moonVisible, skyAt, starsOpacity, type SkyState } from "./sky-ramp";
import { crashTimeline, phaseTransition, type CrashStep, type TransitionKind } from "./timeline";
import { createTrail } from "./trail";
import type { Pausable } from "./visibility";

/** Phase of the scene's script (SPEC-0011 §2). */
export type ScenePhase = "betting" | "takeoff" | "flying" | "crashed" | "restart";

/** Position of the kite on the screen. */
export interface KitePose {
  readonly x: number;
  readonly y: number;
  /** Radians. */
  readonly rotation: number;
  readonly alpha: number;
}

export type ParticleKind = "wind" | "sparkle" | "debris";

export interface Particle {
  readonly kind: ParticleKind;
  readonly x: number;
  readonly y: number;
  /** Direction of travel, radians (the renderer stretches wind streaks along it). */
  readonly angle: number;
  readonly size: number;
  readonly alpha: number;
  /** `0xrrggbb`. */
  readonly color: number;
}

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
  /** Active filters (the kite's glow; the trail glows with drawn strokes, no filter). */
  readonly activeFilters: number;
  /** Renderer resolution (pixel density). */
  readonly resolution: number;
  /** Size of the scene, px. */
  readonly viewport: { readonly width: number; readonly height: number };
  /** Opacity of the moon (0 below 50x). */
  readonly moonOpacity: number;
  readonly kite: KitePose;
  /** Golden glow of the kite from 10x, 0..1 (0 = filter off). */
  readonly kiteGlow: number;
  /** Kite line, hand first. */
  readonly line: readonly Vec2[];
  readonly lineAlpha: number;
  /** Tail (rabiola), kite first. */
  readonly tail: readonly Vec2[];
  /** Trail of the flight curve on the screen, oldest first. */
  readonly trail: readonly Vec2[];
  readonly trailAlpha: number;
  /** Pulse of the last milestone: 1 when it fires → 0. */
  readonly pulse: number;
  readonly pulseMilestone: number;
  readonly particleList: readonly Particle[];
}

export interface PipaAnimatorOptions {
  readonly profile: MotionProfile;
  readonly sky: SkyTheme;
  /** Device pixel ratio of the display (default 1). */
  readonly devicePixelRatio?: number;
  /** Source of randomness for particles and gusts (default Math.random); tests pass a seeded one. */
  readonly random?: () => number;
  /** Keeps the effects log (default true; the production scene turns it off, only the test hook reads it). */
  readonly recordEffects?: boolean;
}

export interface PipaAnimator extends GameAnimator, Pausable {
  readonly profile: MotionProfile;
  /** Advances the scene clock by `dtMs` (ignored while paused) and returns the frame to draw. */
  advance(dtMs: number): SceneFrame;
  /** The current frame, without advancing. */
  frame(): SceneFrame;
  /** Effects started so far, in order. */
  effectsLog(): readonly EffectEntry[];
  /** Size of the scene, px (default 390×844). */
  resize(width: number, height: number): void;
  /**
   * Test hook (SPEC-0011 §6): positions the scene `atMs` after the start of the phase of `state`, deterministically,
   * and stops its clock; from then on the GameAnimator calls are ignored.
   */
  freeze(atMs: number, state: SceneFreezeState): void;
}

/** Shape of the kite drawn by the renderer, px from its centre. */
export const kiteGeometry = { top: 34, bottom: 44, halfWidth: 27, crossY: -6 } as const;

const physicsStep = 1 / 120;
/** Frame time the physics integrates at most per tick, ms (a tab back from a long pause does not fast-forward). */
const maxPhysicsDtMs = 250;
const lineSegments = 20;
const tailSegments = 12;
const tailSegmentLength = 8;
const trailCapacity = 90;
const trailEveryMs = 33;
/** The trail streams back behind the kite, as the wind carries the sky past it, px/ms. */
const trailDriftPerMs = 0.08;
const pulseMs = 700;
/** Growth of the multiplier of SPEC-0008@2, m(t) = e^(k·t) — used by `freeze` to replay a crashed flight. */
const growthPerMs = 0.00006;
const freezeSeed = 20_261_009;
const freezeFrameMs = 1000 / 60;

const colors = {
  wind: 0xffffff,
  gold: 0xfacc15,
  orange: 0xf97316,
  red: 0xef4444,
} as const;

interface MutableParticle {
  kind: ParticleKind;
  x: number;
  y: number;
  vx: number;
  vy: number;
  life: number;
  maxLife: number;
  size: number;
  color: number;
  gravity: number;
}

interface Fall {
  x: number;
  y: number;
  vx: number;
  vy: number;
  rotation: number;
  spin: number;
}

/** Mulberry32: the deterministic randomness of a frozen scene. */
function seeded(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const lerp = (from: number, to: number, t: number): number => from + (to - from) * t;
const clamp01 = (value: number): number => Math.min(1, Math.max(0, value));

function rotate(point: Vec2, angle: number): Vec2 {
  const cos = Math.cos(angle);
  const sin = Math.sin(angle);
  return { x: point.x * cos - point.y * sin, y: point.x * sin + point.y * cos };
}

export function createPipaAnimator(options: PipaAnimatorOptions): PipaAnimator {
  const { profile, sky: theme } = options;
  const reduced = profile === "reduced";
  const resolution = sceneResolution(options.devicePixelRatio ?? 1);
  const recordEffects = options.recordEffects ?? true;
  let random = options.random ?? Math.random;

  let width = 390;
  let height = 844;
  let clock = 0;
  let phase: ScenePhase = "betting";
  let phaseStartMs = 0;
  let script: PhaseScript = restingScript();
  let pendingSteps: CrashStep[] = [];
  let multiplier = 1;
  let crashPoint = 1;
  let restartFrom = 1;
  let paused = false;
  /** prepare() arrived during the crash script: the restart starts when the script ends (the climax is not cut). */
  let restartPending = false;
  let frozen = false;
  /** Replaying for `freeze`: effects are not logged. */
  let quiet = false;
  let pulseStartMs = Number.NEGATIVE_INFINITY;
  let pulseMilestone = 0;
  let lastTrailMs = Number.NEGATIVE_INFINITY;
  let fall: Fall | null = null;
  let fallPendingMs = 0;
  let pose: KitePose = { x: 0, y: 0, rotation: 0, alpha: 1 };
  const log: EffectEntry[] = [];
  let particles: MutableParticle[] = [];
  let windSpawnDebt = 0;

  const milestones = createMilestoneTracker();
  const trail = createTrail(trailCapacity);
  const hand = (): Vec2 => ({ x: width * 0.1, y: height + 24 });
  const newLine = (): VerletRope =>
    createVerletRope({
      anchor: hand(),
      segments: lineSegments,
      segmentLength: 20,
      stepSeconds: physicsStep,
    });
  const newTail = (): VerletRope =>
    createVerletRope({
      anchor: { x: 0, y: 0 },
      segments: tailSegments,
      segmentLength: tailSegmentLength,
      stepSeconds: physicsStep,
      gravity: { x: 0, y: 380 },
    });
  let line = newLine();
  let tail = newTail();
  let lineAttached = true;

  const record = (entry: EffectEntry): void => {
    if (recordEffects && !quiet) log.push(entry);
  };

  // ---- multiplier the visuals follow ----

  const visualMultiplier = (): number => {
    switch (phase) {
      case "betting":
        return 1;
      case "takeoff":
      case "flying":
        return multiplier;
      case "crashed":
        return crashPoint;
      case "restart":
        return Math.exp(Math.log(Math.max(1, restartFrom)) * (1 - script.values.restart));
    }
  };

  const currentWind = (): number => windIntensity(visualMultiplier());

  // ---- kite poses ----

  const swayScale = reduced ? 0.35 : 1;

  const groundPose = (): KitePose => {
    const t = (clock / 2_600) * Math.PI * 2;
    return {
      x: width * 0.3 + Math.sin(t) * 6 * swayScale,
      y: height * 0.62 + Math.sin(t * 2 + 0.7) * 3 * swayScale,
      rotation: -0.1 + Math.sin(t + 0.6) * 0.07 * swayScale,
      alpha: 1,
    };
  };

  const flightPose = (value: number): KitePose => {
    const wind = windIntensity(value);
    const climb = clamp01(Math.log(Math.max(1, value)) / Math.log(20));
    const period = 2_600 - 1_100 * wind;
    const t = (clock / period) * Math.PI * 2;
    const amplitude = (6 + 16 * wind) * swayScale;
    return {
      x: width * (0.42 + 0.2 * climb) + (Math.sin(t) + 0.35 * Math.sin(t * 2.3 + 1.1)) * amplitude,
      y: height * (0.46 - 0.15 * climb) + Math.sin(t * 1.6 + 0.4) * amplitude * 0.45,
      rotation: 0.12 + Math.sin(t + 0.5) * (0.06 + 0.1 * wind) * swayScale,
      alpha: 1,
    };
  };

  const blendPose = (from: KitePose, to: KitePose, t: number): KitePose => ({
    x: lerp(from.x, to.x, t),
    y: lerp(from.y, to.y, t),
    rotation: lerp(from.rotation, to.rotation, t),
    alpha: 1,
  });

  /** Crossfade of the reduced profile: out at `from`, in at `to`. */
  const crossfadePose = (from: KitePose, to: KitePose, t: number): KitePose =>
    t < 0.5 ? { ...from, alpha: 1 - 2 * t } : { ...to, alpha: 2 * t - 1 };

  const livePose = (): KitePose => {
    switch (phase) {
      case "betting":
        return groundPose();
      case "takeoff": {
        const lift = script.values.lift;
        return reduced
          ? crossfadePose(groundPose(), flightPose(multiplier), lift)
          : blendPose(groundPose(), flightPose(multiplier), lift);
      }
      case "flying":
        return flightPose(multiplier);
      case "restart": {
        const restart = script.values.restart;
        const ground = groundPose();
        if (reduced) return { ...ground, alpha: restart };
        return {
          ...ground,
          y: lerp(height + kiteGeometry.top * 2, ground.y, restart),
          alpha: restart,
        };
      }
      case "crashed":
        return fall === null
          ? pose
          : { x: fall.x, y: fall.y, rotation: fall.rotation, alpha: script.values.fade };
    }
  };

  const kitePoint = (local: Vec2): Vec2 => {
    const offset = rotate(local, pose.rotation);
    return { x: pose.x + offset.x, y: pose.y + offset.y };
  };

  // ---- wind fields of the ropes ----

  const lineWind: WindField = (t) => {
    const wind = currentWind();
    return { x: 70 * wind * (1 + 0.5 * Math.sin(t * 1.3)), y: -40 * wind };
  };
  const tailWind: WindField = (t) => {
    const wind = currentWind();
    const gust = reduced ? 0.4 : 1;
    return {
      x:
        (180 + 220 * wind) * (1 + gust * (0.45 * Math.sin(t * 2.1) + 0.25 * Math.sin(t * 5.3 + 1))),
      y: gust * (70 + 90 * wind) * Math.sin(t * 3.7 + 0.3),
    };
  };

  const relayRopes = (): void => {
    line = newLine();
    tail = newTail();
    lineAttached = true;
    pose = livePose();
    const bridle = kitePoint({ x: 0, y: 4 });
    const from = hand();
    line.lay({ x: bridle.x - from.x, y: bridle.y - from.y });
    tail.setAnchor(kitePoint({ x: 0, y: kiteGeometry.bottom }));
    tail.lay({ x: 0.5, y: 1 });
  };

  // ---- particles ----

  const spawn = (particle: Omit<MutableParticle, "maxLife">): void => {
    if (particles.length >= maxParticles) return;
    particles.push({ ...particle, maxLife: particle.life });
  };

  const burst = (kind: "sparkle" | "debris", count: number): void => {
    const palette =
      kind === "sparkle"
        ? [colors.gold, colors.orange, 0xffffff]
        : [colors.red, colors.orange, colors.gold];
    for (let index = 0; index < count; index += 1) {
      const angle = random() * Math.PI * 2;
      const speed = kind === "sparkle" ? 60 + random() * 110 : 90 + random() * 220;
      spawn({
        kind,
        x: pose.x,
        y: pose.y,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed - (kind === "debris" ? 60 : 0),
        life: kind === "sparkle" ? 0.7 + random() * 0.4 : 0.9 + random() * 0.5,
        size: kind === "sparkle" ? 1.5 + random() * 2 : 1.5 + random() * 2.5,
        color: palette[Math.floor(random() * palette.length)] ?? colors.gold,
        gravity: kind === "debris" ? 380 : 40,
      });
    }
  };

  const updateParticles = (dtSeconds: number): void => {
    if (!reduced && phase !== "crashed") {
      // Light wind streaks, more of them as the wind grows.
      windSpawnDebt += dtSeconds * (3 + 22 * currentWind());
      while (windSpawnDebt >= 1) {
        windSpawnDebt -= 1;
        const speed = 260 + 520 * currentWind();
        spawn({
          kind: "wind",
          x: -30,
          y: random() * height,
          vx: speed,
          vy: -speed * 0.08,
          life: (width + 60) / speed,
          size: 1 + random() * 1.2,
          color: colors.wind,
          gravity: 0,
        });
      }
    }
    let alive = 0;
    for (const particle of particles) {
      particle.life -= dtSeconds;
      if (particle.life <= 0) continue;
      particle.vy += particle.gravity * dtSeconds;
      particle.x += particle.vx * dtSeconds;
      particle.y += particle.vy * dtSeconds;
      particles[alive] = particle;
      alive += 1;
    }
    particles.length = alive;
  };

  // ---- phases ----

  const enter = (next: ScenePhase, nextScript: PhaseScript): void => {
    script.kill();
    script = nextScript;
    phase = next;
    phaseStartMs = clock;
    script.seek(0);
  };

  const logTransition = (kind: TransitionKind): void => {
    const transition = phaseTransition(kind, profile);
    record({
      name: "transition",
      atMs: clock,
      transition: kind,
      style: transition.style,
      durationMs: transition.durationMs,
    });
  };

  const logStep = (step: CrashStep): void => {
    const durationMs = step.endMs - step.startMs;
    switch (step.name) {
      case "flash":
        record({ name: "flash", atMs: clock, durationMs, opacity: step.intensity });
        break;
      case "shake":
        record({ name: "shake", atMs: clock, durationMs, amplitudePx: step.intensity });
        break;
      default:
        record({ name: step.name, atMs: clock, durationMs });
    }
  };

  const flushSteps = (phaseTime: number): void => {
    let next = pendingSteps[0];
    while (next !== undefined && next.startMs <= phaseTime) {
      pendingSteps.shift();
      logStep(next);
      next = pendingSteps[0];
    }
  };

  /** A new round from scratch: no fall, no trail, milestones free again. */
  const clearRound = (): void => {
    multiplier = 1;
    milestones.reset();
    trail.clear();
    fall = null;
    pendingSteps = [];
    lastTrailMs = Number.NEGATIVE_INFINITY;
  };

  const startRestart = (): void => {
    restartFrom = visualMultiplier();
    clearRound();
    if (paused) {
      enter("betting", restingScript());
    } else {
      enter("restart", transitionScript("restart", profile));
      logTransition("restart");
    }
    relayRopes();
  };

  const startTakeoff = (): void => {
    restartPending = false;
    const fromCrash = phase === "crashed" || phase === "restart";
    clearRound();
    if (paused) {
      enter("flying", restingScript());
    } else {
      enter("takeoff", transitionScript("takeoff", profile));
      logTransition("takeoff");
      if (!reduced) record({ name: "parallax", atMs: clock });
    }
    if (fromCrash) relayRopes();
    // The pull: the tail whips down and back.
    if (!reduced && !paused) tail.impulse({ x: -160, y: 320 });
  };

  const cutLine = (): void => {
    lineAttached = false;
    line.setEnd(null);
  };

  const startCrash = (point: number): void => {
    restartPending = false;
    crashPoint = Number.isFinite(point) && point > 0 ? point : multiplier;
    pose = livePose();
    enter("crashed", crashScript(profile));
    cutLine();
    if (paused) {
      // Hidden tab: straight to the end of the crash script, nothing replayed.
      phaseStartMs = clock - script.durationMs;
      script.seek(script.durationMs);
      pendingSteps = [];
      fall = { x: pose.x, y: height + 200, vx: 0, vy: 0, rotation: pose.rotation, spin: 0 };
      return;
    }
    pendingSteps = [...crashTimeline(profile)];
    flushSteps(0);
    fall = {
      x: pose.x,
      y: pose.y,
      vx: reduced ? 0 : 50,
      vy: reduced ? 0 : -110,
      rotation: pose.rotation,
      spin: 0,
    };
    fallPendingMs = 0;
    if (!reduced) {
      // The line snaps at the kite and recoils towards the hand.
      const from = hand();
      const toHand = { x: from.x - pose.x, y: from.y - pose.y };
      const norm = Math.hypot(toHand.x, toHand.y) || 1;
      line.impulse({ x: (toHand.x / norm) * 420, y: (toHand.y / norm) * 420 }, lineSegments / 2);
      burst("debris", 36);
    }
  };

  // ---- the tick ----

  const stepFall = (dtMs: number): void => {
    if (fall === null || reduced) return;
    fallPendingMs += dtMs;
    const tumbling = clock - phaseStartMs >= 300;
    const stepMs = physicsStep * 1000;
    while (fallPendingMs >= stepMs - 1e-9) {
      fallPendingMs -= stepMs;
      const dt = physicsStep;
      const gravity = tumbling ? 520 : 140;
      const drag = tumbling ? 0.9 : 2.5;
      fall.vx += (60 * currentWind() - fall.vx * drag) * dt;
      fall.vy += (gravity - fall.vy * drag * 0.6) * dt;
      fall.x += fall.vx * dt;
      fall.y += fall.vy * dt;
      if (tumbling) fall.spin = Math.min(9, fall.spin + 14 * dt);
      fall.rotation += fall.spin * dt;
    }
  };

  const tick = (dtMs: number): void => {
    const dt = Number.isFinite(dtMs) ? Math.max(0, dtMs) : 0;
    clock += dt;
    const physicsSeconds = Math.min(dt, maxPhysicsDtMs) / 1000;
    const phaseTime = clock - phaseStartMs;
    script.seek(phaseTime);
    flushSteps(phaseTime);
    if (phase === "takeoff" && phaseTime >= script.durationMs) enter("flying", restingScript());
    if (phase === "restart" && phaseTime >= script.durationMs) enter("betting", restingScript());
    if (restartPending && phase === "crashed" && phaseTime >= script.durationMs) {
      restartPending = false;
      startRestart();
    }

    stepFall(physicsSeconds * 1000);
    pose = livePose();

    // Ropes: the line pays out between the hand and the kite, the tail hangs from the kite.
    const from = hand();
    line.setAnchor(from);
    if (lineAttached) {
      const bridle = kitePoint({ x: 0, y: 4 });
      const distance = Math.hypot(bridle.x - from.x, bridle.y - from.y);
      const slack = 1.05 - 0.04 * currentWind() - (phase === "takeoff" ? 0.03 : 0);
      line.setSegmentLength((distance / lineSegments) * slack);
      line.setEnd(bridle);
    }
    line.advance(physicsSeconds, lineWind);
    tail.setAnchor(kitePoint({ x: 0, y: kiteGeometry.bottom }));
    tail.advance(physicsSeconds, tailWind);

    if ((phase === "takeoff" || phase === "flying") && clock - lastTrailMs >= trailEveryMs) {
      lastTrailMs = clock;
      trail.push({
        x: pose.x + clock * trailDriftPerMs,
        y: pose.y - cameraAltitude(visualMultiplier()) * height,
      });
    }
    updateParticles(physicsSeconds);
  };

  // ---- the frame ----

  const frame = (): SceneFrame => {
    const shown = visualMultiplier();
    const altitude = cameraAltitude(shown);
    const values = script.values;
    const crashed = phase === "crashed";
    const shakeAmplitude = crashed && !reduced ? values.shake : 0;
    const angle = (clock - phaseStartMs) * 0.09;
    const shake =
      shakeAmplitude > 0
        ? { x: shakeAmplitude * Math.cos(angle), y: shakeAmplitude * Math.sin(angle) }
        : { x: 0, y: 0 };
    const camera = altitude * height;
    const trailPoints = trail
      .points()
      .map((point) => ({ x: point.x - clock * trailDriftPerMs, y: point.y + camera }));
    const trailAlpha = crashed ? values.fade : phase === "restart" ? 0 : 1;
    const kiteGlow =
      (phase === "flying" || phase === "takeoff") && shown >= 10
        ? 0.35 + 0.65 * clamp01(Math.log(shown / 10) / Math.log(10))
        : 0;
    const pulseAge = clock - pulseStartMs;
    return {
      timeMs: clock,
      phase,
      multiplier:
        phase === "betting" || phase === "restart" ? 1 : crashed ? crashPoint : multiplier,
      sky: skyAt(shown, theme),
      stars: starsOpacity(shown),
      moon: moonVisible(shown),
      cameraAltitude: altitude,
      wind: windIntensity(shown),
      clouds: cloudLayers(altitude, clock, profile, height),
      shake,
      flashOpacity: crashed && !reduced ? values.flash : 0,
      particles: particles.length,
      activeFilters: Math.min(maxActiveFilters, kiteGlow > 0 ? 1 : 0),
      resolution,
      viewport: { width, height },
      moonOpacity: moonOpacity(shown),
      kite: pose,
      kiteGlow,
      line: line.points(),
      lineAlpha: lineAttached ? 1 : crashed ? values.fade : 0,
      tail: tail.points(),
      trail: trailPoints,
      trailAlpha,
      pulse: pulseAge >= 0 && pulseAge < pulseMs ? 1 - pulseAge / pulseMs : 0,
      pulseMilestone,
      particleList: particles.map((particle) => ({
        kind: particle.kind,
        x: particle.x,
        y: particle.y,
        angle: Math.atan2(particle.vy, particle.vx),
        size: particle.size,
        alpha: particle.kind === "wind" ? 0.35 : clamp01(particle.life / particle.maxLife),
        color: particle.color,
      })),
    };
  };

  // ---- GameAnimator ----

  const prepare = (): void => {
    if (frozen || phase === "betting" || phase === "restart") return;
    if (phase === "crashed" && !paused && clock - phaseStartMs < script.durationMs) {
      restartPending = true;
      return;
    }
    startRestart();
  };

  const takeoff = (): void => {
    if (frozen || phase === "takeoff" || phase === "flying") return;
    startTakeoff();
  };

  const setMultiplier = (value: number): void => {
    if (frozen || (phase !== "takeoff" && phase !== "flying")) return;
    if (!Number.isFinite(value) || value <= 0) return;
    multiplier = value;
    for (const milestone of milestones.update(value)) {
      if (paused) continue;
      pulseStartMs = clock;
      pulseMilestone = milestone;
      record({ name: "pulse", atMs: clock, milestone });
      if (!reduced) burst("sparkle", milestone >= 10 ? 28 : 18);
    }
  };

  const crash = (point: number): void => {
    if (frozen || phase === "crashed") return;
    startCrash(point);
  };

  /** Back to the very first scene (clock 0, betting), for `freeze`. */
  const restore = (): void => {
    restartPending = false;
    script.kill();
    script = restingScript();
    clock = 0;
    phase = "betting";
    phaseStartMs = 0;
    clearRound();
    crashPoint = 1;
    restartFrom = 1;
    pulseStartMs = Number.NEGATIVE_INFINITY;
    pulseMilestone = 0;
    fallPendingMs = 0;
    particles = [];
    windSpawnDebt = 0;
    relayRopes();
  };

  relayRopes();

  return {
    profile,
    prepare,
    takeoff,
    setMultiplier,
    crash,
    reset: prepare,
    pause() {
      paused = true;
    },
    resume() {
      paused = false;
    },
    advance(dtMs) {
      if (!paused && !frozen) tick(dtMs);
      return frame();
    },
    frame,
    effectsLog() {
      return log;
    },
    resize(nextWidth, nextHeight) {
      if (!(nextWidth > 0) || !(nextHeight > 0)) return;
      if (nextWidth === width && nextHeight === height) return;
      width = nextWidth;
      height = nextHeight;
      relayRopes();
    },
    freeze(atMs, state) {
      frozen = false;
      paused = false;
      quiet = true;
      random = seeded(freezeSeed);
      restore();
      const run = (durationMs: number, onFrame?: (elapsedMs: number) => void): void => {
        for (let elapsed = 0; elapsed < durationMs; elapsed += freezeFrameMs) {
          onFrame?.(elapsed);
          tick(Math.min(freezeFrameMs, durationMs - elapsed));
        }
      };
      const target = Math.max(1, state.multiplier);
      const flyTo = (durationMs: number): void => {
        run(durationMs, (elapsed) => {
          setMultiplier(Math.pow(target, elapsed / durationMs));
        });
        setMultiplier(target);
      };
      switch (state.phase) {
        case "betting":
          run(atMs);
          break;
        case "flying":
          run(1_000);
          takeoff();
          flyTo(atMs);
          break;
        case "crashed":
          run(1_000);
          takeoff();
          flyTo(Math.log(target) / growthPerMs);
          crash(target);
          run(atMs);
          break;
      }
      quiet = false;
      frozen = true;
    },
  };
}
