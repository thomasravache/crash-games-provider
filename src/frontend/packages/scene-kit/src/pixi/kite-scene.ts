// Code animation of the kite (ADR-0004, ADR-0021) behind GameAnimator: the kite climbs with the multiplier on its
// line, the sky goes from day to night, and on the crash the line is cut and the kite spins away. Decorative only
// (ADR-0020): the canvas is hidden from assistive technology and the HUD announces the states.

import { Application, Container, FillGradient, Graphics, type Ticker } from "pixi.js";

import type { GameAnimator } from "../game-animator";
import { skyForMultiplier, type SkyPalette, type SkyTheme } from "../sky";

export interface KiteSceneOptions {
  readonly sky: SkyTheme;
  /** Kite colours, `0xrrggbb`. */
  readonly kite?: { readonly body: number; readonly accent: number; readonly line: number };
}

export interface KiteScene {
  readonly animator: GameAnimator;
  destroy(): void;
}

type Mode = "ground" | "flying" | "falling";

/** Multiplier at which the kite reaches the top of its path. */
const topMultiplier = 50;

const defaultKite = { body: 0xf97316, accent: 0xfacc15, line: 0xe0e1f2 };

function prefersReducedMotion(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function drawKite(colors: typeof defaultKite): Container {
  const kite = new Container();
  const tail = new Graphics()
    .moveTo(0, 26)
    .bezierCurveTo(-10, 44, 10, 58, -4, 76)
    .stroke({ width: 2, color: colors.accent });
  const body = new Graphics()
    .poly([0, -24, 18, 0, 0, 28, -18, 0])
    .fill(colors.body)
    .moveTo(0, -24)
    .lineTo(0, 28)
    .moveTo(-18, 0)
    .lineTo(18, 0)
    .stroke({ width: 1.5, color: colors.accent, alpha: 0.9 });
  kite.addChild(tail, body);
  return kite;
}

/** Creates the PixiJS application inside `host` and returns the GameAnimator that drives it. */
export async function createKiteScene(
  host: HTMLElement,
  options: KiteSceneOptions,
): Promise<KiteScene> {
  const colors = options.kite ?? defaultKite;
  const app = new Application();
  await app.init({
    resizeTo: host,
    backgroundAlpha: 0,
    antialias: true,
    autoDensity: true,
    resolution: Math.min(globalThis.devicePixelRatio || 1, 2),
  });
  app.canvas.setAttribute("aria-hidden", "true");
  app.canvas.style.display = "block";
  host.appendChild(app.canvas);

  const reducedMotion = prefersReducedMotion();
  const sky = new Graphics();
  const line = new Graphics();
  const kite = drawKite(colors);
  app.stage.addChild(sky, line, kite);

  let mode: Mode = "ground";
  let multiplier = 1;
  let palette: SkyPalette | undefined;
  let gradient: FillGradient | undefined;
  let elapsed = 0;
  let fall = { x: 0, y: 0, vx: 0, vy: 0, spin: 0 };

  const size = (): { width: number; height: number } => app.renderer.screen;
  const anchor = (): { x: number; y: number } => ({ x: size().width * 0.08, y: size().height });

  /** Position of the kite on its path for the multiplier (0 = ground, 1 = top). */
  const pathPoint = (value: number): { x: number; y: number } => {
    const progress = Math.min(1, Math.log(Math.max(1, value)) / Math.log(topMultiplier));
    const { width, height } = size();
    return { x: width * (0.22 + 0.5 * progress), y: height * (0.78 - 0.56 * progress) };
  };

  const drawSky = (force = false): void => {
    const next = skyForMultiplier(multiplier, options.sky);
    if (!force && palette?.top === next.top && palette.bottom === next.bottom) return;
    palette = next;
    gradient?.destroy();
    gradient = new FillGradient({
      type: "linear",
      start: { x: 0, y: 0 },
      end: { x: 0, y: 1 },
      colorStops: [
        { offset: 0, color: next.top },
        { offset: 1, color: next.bottom },
      ],
      textureSpace: "local",
    });
    const { width, height } = size();
    sky.clear().rect(0, 0, width, height).fill(gradient);
  };

  const drawLine = (to: { x: number; y: number } | null): void => {
    line.clear();
    const from = anchor();
    if (to === null) {
      // Cut line: a short loose end hanging from the hand.
      line
        .moveTo(from.x, from.y)
        .quadraticCurveTo(from.x + 30, from.y - 50, from.x + 46, from.y - 30);
    } else {
      line
        .moveTo(from.x, from.y)
        .quadraticCurveTo((from.x + to.x) / 2, to.y + (from.y - to.y) * 0.75, to.x, to.y + 26);
    }
    line.stroke({ width: 1.5, color: colors.line, alpha: 0.85 });
  };

  const update = (ticker: Ticker): void => {
    elapsed += ticker.deltaMS;
    if (mode === "falling") {
      const dt = ticker.deltaMS / 1000;
      fall = {
        ...fall,
        x: fall.x + fall.vx * dt,
        y: fall.y + fall.vy * dt,
        vy: fall.vy + 260 * dt,
        spin: fall.spin + (reducedMotion ? 0 : 9 * dt),
      };
      kite.position.set(fall.x, fall.y);
      kite.rotation = fall.spin;
      kite.alpha = Math.max(0, kite.alpha - dt * 0.5);
      drawLine(null);
      return;
    }
    const point = pathPoint(multiplier);
    const sway = reducedMotion || mode === "ground" ? 0 : Math.sin(elapsed / 420) * 6;
    kite.position.set(point.x + sway, point.y + Math.cos(elapsed / 530) * (sway / 2));
    kite.rotation = mode === "ground" ? 0 : Math.sin(elapsed / 700) * 0.12;
    kite.alpha = 1;
    drawLine({ x: kite.position.x, y: kite.position.y });
  };

  const onResize = (): void => {
    drawSky(true);
  };
  app.renderer.on("resize", onResize);
  app.ticker.add(update);
  drawSky(true);

  const animator: GameAnimator = {
    prepare() {
      mode = "ground";
      multiplier = 1;
      drawSky();
    },
    takeoff() {
      mode = "flying";
      elapsed = 0;
    },
    setMultiplier(value) {
      if (mode === "falling") return;
      mode = "flying";
      multiplier = value;
      drawSky();
    },
    crash(point) {
      multiplier = point;
      drawSky();
      mode = "falling";
      fall = { x: kite.position.x, y: kite.position.y, vx: 90, vy: -40, spin: kite.rotation };
    },
    reset() {
      mode = "ground";
      multiplier = 1;
      drawSky();
    },
  };

  return {
    animator,
    destroy() {
      app.renderer.off("resize", onResize);
      app.ticker.remove(update);
      gradient?.destroy();
      app.destroy({ removeView: true }, { children: true });
    },
  };
}
