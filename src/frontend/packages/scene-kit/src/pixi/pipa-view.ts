// Display objects of the Pipa scene (SPEC-0011 §2): vector art drawn in code — sky gradients, sun, stars, moon, three
// cloud layers, the Rio waterfront with its hills, the 4-colour diamond kite of the Stitch design ("Pipa Crash"), its
// line, tail and trail, particles and the crash flash. `draw` only moves, tints and fades what was built; the few
// shapes that change every frame (line, tail, trail) are short polylines.

import { GlowFilter } from "pixi-filters/glow";
import { Container, FillGradient, Graphics, Sprite, type Renderer, type Texture } from "pixi.js";

import { kiteGeometry, type SceneFrame } from "../kite/pipa-animator";
import type { MotionProfile } from "../kite/motion-profile";
import { maxParticles } from "../kite/effects";
import type { Vec2 } from "../kite/kite-rig";

/** Colour stops (top → horizon) of the three skies, `#rrggbb`. */
export interface SkyGradients {
  readonly day: readonly string[];
  readonly sunset: readonly string[];
  readonly night: readonly string[];
}

/** Colours of the Stitch design system "Pipa Crash". */
const palette = {
  gold: 0xfacc15,
  orange: 0xf97316,
  magenta: 0xec4899,
  violet: 0x7c3aed,
  flash: 0xef4444,
  line: 0xe0e1f2,
  trailCore: 0xfdba74,
  farHills: 0x3b2a6b,
  nearHills: 0x24164f,
  waterfront: 0x140b2e,
  sea: 0x1e3a8a,
  moon: 0xf5f3ff,
  sunDay: 0xfde68a,
  sunset: 0xf97316,
  cloudDay: 0xffffff,
  cloudNight: 0x5b4b8a,
} as const;

interface Cloud {
  readonly shape: Graphics;
  readonly layer: number;
  /** Position as a fraction of the screen. */
  readonly x: number;
  readonly y: number;
}

/** Deterministic layout randomness (the same stars and clouds on every run, so the keyframes are reproducible). */
function layoutRandom(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function mixRgb(from: number, to: number, t: number): number {
  const k = Math.min(1, Math.max(0, t));
  const channel = (shift: number): number => {
    const a = (from >> shift) & 0xff;
    const b = (to >> shift) & 0xff;
    return Math.round(a + (b - a) * k) << shift;
  };
  return channel(16) | channel(8) | channel(0);
}

const clamp01 = (value: number): number => Math.min(1, Math.max(0, value));

function drawKite(): Graphics {
  const { top, bottom, halfWidth, crossY } = kiteGeometry;
  const t = { x: 0, y: -top };
  const r = { x: halfWidth, y: crossY };
  const b = { x: 0, y: bottom };
  const l = { x: -halfWidth, y: crossY };
  const c = { x: 0, y: crossY };
  const quad = (points: readonly { x: number; y: number }[]): number[] =>
    points.flatMap((p) => [p.x, p.y]);
  return new Graphics()
    .poly(quad([t, c, l]))
    .fill(palette.gold)
    .poly(quad([t, r, c]))
    .fill(palette.orange)
    .poly(quad([c, r, b]))
    .fill(palette.magenta)
    .poly(quad([l, c, b]))
    .fill(palette.violet)
    .poly(quad([t, r, b, l]))
    .stroke({ width: 1.5, color: 0x1f1147, alpha: 0.55 })
    .moveTo(t.x, t.y)
    .lineTo(b.x, b.y)
    .moveTo(l.x, l.y)
    .lineTo(r.x, r.y)
    .stroke({ width: 1.6, color: 0xfff7ed, alpha: 0.85 })
    .poly(quad([t, { x: 8, y: -20 }, { x: 0, y: -14 }]))
    .fill({ color: 0xffffff, alpha: 0.22 });
}

function drawCloud(random: () => number, scale: number): Graphics {
  const cloud = new Graphics();
  const puffs = 4 + Math.floor(random() * 3);
  for (let index = 0; index < puffs; index += 1) {
    const x = (index - puffs / 2) * 22 * scale + random() * 8 * scale;
    const y = -random() * 14 * scale;
    cloud.circle(x, y, (16 + random() * 14) * scale);
  }
  cloud.roundRect(
    (-puffs / 2) * 22 * scale - 14 * scale,
    -6 * scale,
    puffs * 22 * scale + 20 * scale,
    20 * scale,
    10 * scale,
  );
  return cloud.fill(0xffffff);
}

/** The Rio waterfront at the bottom of the screen (0 = the bottom edge): hills, Sugarloaf, sea, buildings, palms. */
function drawGround(width: number): Container {
  const ground = new Container();
  const hills = new Graphics()
    .moveTo(0, -150)
    .bezierCurveTo(width * 0.12, -190, width * 0.2, -260, width * 0.3, -300)
    .lineTo(width * 0.33, -318)
    .lineTo(width * 0.36, -296)
    .bezierCurveTo(width * 0.46, -240, width * 0.55, -190, width * 0.66, -170)
    .bezierCurveTo(width * 0.8, -150, width * 0.9, -180, width, -160)
    .lineTo(width, 0)
    .lineTo(0, 0)
    .closePath()
    .fill({ color: palette.farHills, alpha: 0.7 });
  // Christ the Redeemer on the tallest peak: a tiny cross.
  const statue = new Graphics()
    .rect(width * 0.33 - 1.2, -334, 2.4, 16)
    .rect(width * 0.33 - 6, -330, 12, 2.2)
    .fill({ color: palette.farHills, alpha: 0.9 });
  // Sugarloaf: two domes on the right, over the bay.
  const sugarloaf = new Graphics()
    .moveTo(width * 0.58, -60)
    .bezierCurveTo(width * 0.62, -120, width * 0.66, -150, width * 0.7, -150)
    .bezierCurveTo(width * 0.73, -150, width * 0.74, -120, width * 0.76, -100)
    .bezierCurveTo(width * 0.8, -150, width * 0.86, -240, width * 0.92, -238)
    .bezierCurveTo(width * 0.97, -236, width, -170, width * 1.02, -60)
    .closePath()
    .fill({ color: palette.nearHills, alpha: 0.92 });
  const cable = new Graphics()
    .moveTo(width * 0.7, -150)
    .lineTo(width * 0.92, -238)
    .stroke({ width: 0.8, color: palette.nearHills, alpha: 0.9 });
  const sea = new Graphics().rect(0, -66, width, 30).fill({ color: palette.sea, alpha: 0.45 });
  const random = layoutRandom(7);
  for (let index = 0; index < 9; index += 1) {
    const x = random() * width;
    sea.rect(x, -60 + random() * 20, 18 + random() * 30, 1).fill({ color: 0xffffff, alpha: 0.18 });
  }
  const city = new Graphics();
  let x = -4;
  while (x < width) {
    const w = 10 + random() * 18;
    const h = 14 + random() * 30;
    city.rect(x, -36 - h, w, h + 36);
    x += w + 1 + random() * 3;
  }
  city.fill(palette.waterfront);
  // Copacabana promenade: the wave pattern along the beach.
  const promenade = new Graphics().moveTo(0, -12);
  for (let wx = 0; wx <= width; wx += 12)
    promenade.quadraticCurveTo(wx + 6, wx % 24 === 0 ? -18 : -6, wx + 12, -12);
  promenade.stroke({ width: 1.2, color: 0xffffff, alpha: 0.16 });
  const palms = new Graphics();
  for (const px of [0.06, 0.2, 0.47, 0.82]) {
    const baseX = width * px;
    palms
      .moveTo(baseX, -30)
      .quadraticCurveTo(baseX + 4, -52, baseX + 2, -74)
      .stroke({ width: 3, color: palette.waterfront });
    for (const angle of [-2.6, -2.1, -1.4, -0.9, -0.4]) {
      palms
        .moveTo(baseX + 2, -74)
        .quadraticCurveTo(
          baseX + 2 + Math.cos(angle) * 12,
          -74 + Math.sin(angle) * 12 - 4,
          baseX + 2 + Math.cos(angle) * 22,
          -74 + Math.sin(angle) * 18 + 8,
        )
        .stroke({ width: 2.4, color: palette.waterfront });
    }
  }
  ground.addChild(hills, statue, sugarloaf, cable, sea, city, promenade, palms);
  return ground;
}

function skyGradient(stops: readonly string[]): FillGradient {
  return new FillGradient({
    type: "linear",
    start: { x: 0, y: 0 },
    end: { x: 0, y: 1 },
    colorStops: stops.map((color, index) => ({
      offset: index / Math.max(1, stops.length - 1),
      color,
    })),
    textureSpace: "local",
  });
}

function polyline(graphics: Graphics, points: readonly Vec2[]): Graphics {
  const [first, ...rest] = points;
  if (first === undefined) return graphics;
  graphics.moveTo(first.x, first.y);
  for (const point of rest) graphics.lineTo(point.x, point.y);
  return graphics;
}

export interface PipaView {
  /** Rebuilds the art for a new size (and after a lost WebGL context). */
  layout(width: number, height: number): void;
  draw(frame: SceneFrame): void;
  destroy(): void;
}

/**
 * Builds the view on `stage`. `filterWarmUpFrames`: frames drawn without filters first — a renderer recreated after a
 * lost context shows the scene at once and compiles the filter shaders later (slow on software GL).
 */
export function createPipaView(
  stage: Container,
  renderer: Renderer,
  gradients: SkyGradients,
  profile: MotionProfile,
  filterWarmUpFrames = 0,
): PipaView {
  const world = new Container();
  const flash = new Graphics();
  stage.addChild(world, flash);

  const kiteGlow = new GlowFilter({
    distance: 12,
    outerStrength: 0,
    color: palette.gold,
    quality: 0.2,
  });

  let skies: Graphics[] = [];
  let sun = new Graphics();
  let stars = new Graphics();
  let moon = new Container();
  let ground = new Container();
  let clouds: Cloud[] = [];
  const cloudLayers: Container[] = [new Container(), new Container(), new Container()];
  const trail = new Graphics();
  const line = new Graphics();
  const tail = new Graphics();
  const kite = new Container();
  const pulse = new Graphics();
  const particles = new Container();
  let dot: Texture | null = null;
  let sprites: Sprite[] = [];
  let width = 0;
  let height = 0;
  let kiteGlowOn = false;
  let framesDrawn = 0;
  let skyFills: FillGradient[] = [];

  const clear = (): void => {
    // The layers that live as long as the view are only detached; the art of the layout is destroyed.
    const kept: readonly Container[] = [...cloudLayers, trail, line, tail, kite, pulse, particles];
    for (const child of world.removeChildren()) {
      if (!kept.includes(child)) child.destroy({ children: true });
    }
    for (const layer of cloudLayers) for (const child of layer.removeChildren()) child.destroy();
    for (const child of kite.removeChildren()) child.destroy();
    for (const sprite of particles.removeChildren()) sprite.destroy();
    dot?.destroy(true);
    dot = null;
    for (const fill of skyFills) fill.destroy();
    skyFills = [];
    sprites = [];
    clouds = [];
  };

  const layout = (nextWidth: number, nextHeight: number): void => {
    clear();
    width = nextWidth;
    height = nextHeight;
    skies = [gradients.day, gradients.sunset, gradients.night].map((stops) => {
      const fill = skyGradient(stops);
      skyFills.push(fill);
      return new Graphics().rect(0, 0, width, height).fill(fill);
    });

    sun = new Graphics()
      .circle(0, 0, 70)
      .fill({ color: 0xffffff, alpha: 0.08 })
      .circle(0, 0, 48)
      .fill({ color: 0xffffff, alpha: 0.14 })
      .circle(0, 0, 32)
      .fill(0xffffff);

    const random = layoutRandom(2026);
    stars = new Graphics();
    for (let index = 0; index < 90; index += 1) {
      stars.circle(random() * width, random() * height * 0.85, 0.5 + random() * 1.2);
    }
    stars.fill(0xffffff);

    moon = new Container();
    moon.addChild(
      new Graphics()
        .circle(0, 0, 60)
        .fill({ color: palette.moon, alpha: 0.05 })
        .circle(0, 0, 40)
        .fill({ color: palette.moon, alpha: 0.09 })
        .circle(0, 0, 26)
        .fill(palette.moon)
        .circle(-8, -5, 5)
        .circle(7, 6, 3.5)
        .circle(4, -10, 2.5)
        .fill({ color: 0xc4b5fd, alpha: 0.45 }),
    );
    moon.position.set(width * 0.76, height * 0.15);

    const cloudRandom = layoutRandom(11);
    clouds = [];
    cloudLayers.forEach((layer, index) => {
      const count = [5, 4, 3][index] ?? 3;
      for (let cloudIndex = 0; cloudIndex < count; cloudIndex += 1) {
        const shape = drawCloud(cloudRandom, 0.6 + 0.35 * index);
        shape.alpha = [0.45, 0.6, 0.8][index] ?? 0.6;
        layer.addChild(shape);
        clouds.push({ shape, layer: index, x: cloudRandom(), y: cloudRandom() });
      }
    });

    // The waterfront keeps its proportions and takes at most about half of a short scene.
    const groundScale = Math.min(1, height / 700);
    ground = drawGround(width / groundScale);
    ground.scale.set(groundScale);

    kite.addChild(drawKite());
    pulse.clear().circle(0, 0, 36).stroke({ width: 3, color: 0xffffff });
    dot = renderer.generateTexture(new Graphics().circle(8, 8, 8).fill(0xffffff));
    sprites = [];
    for (let index = 0; index < maxParticles; index += 1) {
      const sprite = new Sprite(dot);
      sprite.anchor.set(0.5);
      sprite.visible = false;
      sprites.push(sprite);
      particles.addChild(sprite);
    }
    flash.clear().rect(0, 0, width, height).fill(palette.flash);

    const [far, middle, near] = cloudLayers;
    world.addChild(...skies, sun, stars, moon);
    if (far !== undefined) world.addChild(far);
    if (middle !== undefined) world.addChild(middle);
    world.addChild(ground, trail, line, tail, kite, pulse, particles);
    if (near !== undefined) world.addChild(near);
  };

  const drawClouds = (frame: SceneFrame): void => {
    const span = height * 1.6;
    const margin = 140;
    const cloudTint = mixRgb(palette.cloudDay, palette.cloudNight, frame.sky.progress / 2);
    const warm = frame.sky.progress > 0.5 && frame.sky.progress < 1.8;
    for (const cloud of clouds) {
      const layer = frame.clouds[cloud.layer];
      const offset = layer?.offsetY ?? 0;
      const drift =
        profile === "reduced"
          ? 0
          : frame.timeMs * (0.004 + 0.006 * cloud.layer) * (0.5 + frame.wind);
      const y = (((cloud.y * span + offset) % span) + span) % span;
      const x =
        (((cloud.x * (width + 2 * margin) + drift) % (width + 2 * margin)) + width + 2 * margin) %
        (width + 2 * margin);
      cloud.shape.position.set(x - margin, y - height * 0.45);
      cloud.shape.tint = warm ? mixRgb(cloudTint, 0xfed7aa, 0.35) : cloudTint;
    }
  };

  const draw = (frame: SceneFrame): void => {
    if (dot === null) return;
    const progress = frame.sky.progress;
    world.position.set(frame.shake.x, frame.shake.y);
    const [day, sunset, night] = skies;
    if (day !== undefined) day.alpha = 1;
    if (sunset !== undefined) sunset.alpha = clamp01(progress);
    if (night !== undefined) night.alpha = clamp01(progress - 1);

    sun.alpha = 1 - clamp01((progress - 1.4) / 0.5);
    sun.visible = sun.alpha > 0;
    sun.tint = mixRgb(palette.sunDay, palette.sunset, progress);
    sun.position.set(width * 0.8, height * (0.16 + 0.56 * clamp01(progress / 1.8)));

    stars.alpha = frame.stars;
    stars.visible = frame.stars > 0;
    moon.alpha = frame.moonOpacity;
    moon.visible = frame.moonOpacity > 0;

    drawClouds(frame);

    const camera = frame.cameraAltitude * height;
    ground.position.set(0, height + camera);
    ground.visible = camera < height + 400;

    trail.clear();
    trail.alpha = frame.trailAlpha;
    if (frame.trail.length > 1) {
      polyline(trail, frame.trail).stroke({
        width: 9,
        color: palette.orange,
        alpha: 0.28,
        cap: "round",
        join: "round",
      });
      polyline(trail, frame.trail).stroke({
        width: 3,
        color: palette.trailCore,
        alpha: 0.95,
        cap: "round",
        join: "round",
      });
    }

    line.clear();
    line.alpha = frame.lineAlpha;
    if (frame.lineAlpha > 0)
      polyline(line, frame.line).stroke({ width: 1.4, color: palette.line, alpha: 0.9 });

    tail.clear();
    tail.alpha = frame.kite.alpha;
    polyline(tail, frame.tail).stroke({ width: 1.6, color: palette.orange, alpha: 0.95 });
    frame.tail.forEach((point, index) => {
      if (index === 0 || index % 2 !== 0) return;
      const color = index % 4 === 0 ? palette.gold : palette.magenta;
      tail.poly([point.x - 5, point.y - 3, point.x, point.y, point.x - 5, point.y + 3]).fill(color);
      tail.poly([point.x + 5, point.y - 3, point.x, point.y, point.x + 5, point.y + 3]).fill(color);
    });

    kite.position.set(frame.kite.x, frame.kite.y);
    kite.rotation = frame.kite.rotation;
    kite.alpha = frame.kite.alpha;
    kite.visible = frame.kite.alpha > 0;
    framesDrawn += 1;
    const glowOn = frame.kiteGlow > 0 && framesDrawn > filterWarmUpFrames;
    if (glowOn !== kiteGlowOn) {
      kiteGlowOn = glowOn;
      kite.filters = glowOn ? [kiteGlow] : [];
    }
    kiteGlow.outerStrength = 1 + 3 * frame.kiteGlow;

    pulse.visible = frame.pulse > 0;
    pulse.position.set(frame.kite.x, frame.kite.y);
    pulse.scale.set(1 + (1 - frame.pulse) * (profile === "reduced" ? 0.2 : 1.8));
    pulse.alpha = frame.pulse * 0.85;
    pulse.tint = frame.pulseMilestone >= 10 ? palette.gold : 0xffffff;

    for (let index = 0; index < sprites.length; index += 1) {
      const sprite = sprites[index];
      if (sprite === undefined) continue;
      const particle = frame.particleList[index];
      if (particle === undefined) {
        sprite.visible = false;
        continue;
      }
      sprite.visible = true;
      sprite.position.set(particle.x, particle.y);
      sprite.tint = particle.color;
      sprite.alpha = particle.alpha;
      sprite.rotation = particle.angle;
      const size = particle.size / 8;
      if (particle.kind === "wind") sprite.scale.set(size * 9, size * 0.6);
      else sprite.scale.set(size);
    }

    flash.alpha = frame.flashOpacity;
    flash.visible = frame.flashOpacity > 0;
  };

  return {
    layout,
    draw,
    destroy() {
      clear();
      kiteGlow.destroy();
      for (const layer of cloudLayers) layer.destroy();
      trail.destroy();
      line.destroy();
      tail.destroy();
      kite.destroy();
      pulse.destroy();
      particles.destroy();
      world.destroy();
      flash.destroy();
    },
  };
}
