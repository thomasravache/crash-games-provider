// The Pipa scene in PixiJS (SPEC-0011, ADR-0004, ADR-0021): the PipaAnimator advanced by its own ticker and drawn by
// the PipaView. Pauses with the tab hidden. A lost WebGL context (SPEC-0011 §5, IT-02) stops the drawing and the
// renderer and the art are recreated on a fresh canvas — right away when the browser allows a new context, otherwise
// when it restores the lost one — without reloading the page; the scene goes on from the animator's current state. Decorative only (ADR-0020): the canvas is hidden from assistive
// technology and the HUD announces the states.

import { Application, Ticker } from "pixi.js";

import type { SkyTheme } from "../sky";
import { sceneResolution } from "../kite/effects";
import { motionProfileFrom, type MotionProfile } from "../kite/motion-profile";
import { createPipaAnimator, type PipaAnimator } from "../kite/pipa-animator";
import { pauseWhenHidden } from "../kite/visibility";
import { createPipaView, type PipaView, type SkyGradients } from "./pipa-view";

export type { SkyGradients } from "./pipa-view";

export interface PipaSceneOptions {
  readonly sky: SkyTheme;
  readonly gradients: SkyGradients;
  /** Default: from `prefers-reduced-motion`. */
  readonly profile?: MotionProfile;
  /** Keeps the effects log of the animator (test builds). */
  readonly recordEffects?: boolean;
  /** Called after each rendered frame with the work it took (advance + draw + render), ms. */
  readonly onFrame?: (workMs: number) => void;
}

export interface PipaScene {
  readonly animator: PipaAnimator;
  destroy(): void;
}

/** Frames a recreated renderer draws before turning the filters on again (about 2 s at 60 fps). */
const filterWarmUpFrames = 120;
/** Textures per batch: sky, the white of the shapes, the particle dot and a spare. */
const maxBatchTextures = 4;

/** A renderer on its own canvas with the art of the scene. */
interface Renderer {
  readonly app: Application;
  readonly view: PipaView;
  dispose(): void;
}

/** Creates the PixiJS application inside `host` and returns the animator that drives it. */
export async function createPipaScene(
  host: HTMLElement,
  options: PipaSceneOptions,
): Promise<PipaScene> {
  const devicePixelRatio = globalThis.devicePixelRatio || 1;
  const profile =
    options.profile ?? motionProfileFrom(typeof window === "undefined" ? undefined : window);
  const animator = createPipaAnimator({
    profile,
    sky: options.sky,
    devicePixelRatio,
    ...(options.recordEffects === undefined ? {} : { recordEffects: options.recordEffects }),
  });

  let current: Renderer | null = null;
  let hidden = false;
  let contextLost = false;
  let destroyed = false;
  const isDestroyed = (): boolean => destroyed;
  const ticker = new Ticker();
  /** Frames presented since the scene froze (or since the last layout). */
  let stillFrames = 0;

  const sync = (): void => {
    if (!hidden && !contextLost && current !== null) ticker.start();
    else ticker.stop();
  };

  const createRenderer = async (recreated: boolean): Promise<Renderer> => {
    const app = new Application();
    await app.init({
      preference: "webgl",
      resizeTo: host,
      // Opaque (the sky covers it) and without MSAA: the scene is fill-rate bound on software GL and mid-range phones.
      background: 0x080b16,
      antialias: false,
      autoDensity: true,
      autoStart: false,
      sharedTicker: false,
      resolution: sceneResolution(devicePixelRatio),
    });
    // The scene uses a handful of textures: a short batch shader is much cheaper to compile (on a fresh context) and
    // to run per pixel on software GL than the default one branching over every texture unit.
    app.renderer.limits.maxBatchableTextures = Math.min(
      maxBatchTextures,
      app.renderer.limits.maxBatchableTextures,
    );
    app.ticker.stop();
    const canvas = app.canvas;
    canvas.setAttribute("aria-hidden", "true");
    canvas.style.display = "block";
    host.appendChild(canvas);

    const view = createPipaView(
      app.stage,
      app.renderer,
      options.gradients,
      profile,
      recreated ? filterWarmUpFrames : 0,
    );
    const layout = (): void => {
      stillFrames = 0;
      const { width, height } = app.renderer.screen;
      animator.resize(width, height);
      view.layout(width, height);
    };
    layout();
    app.renderer.on("resize", layout);

    const onContextLost = (): void => {
      contextLost = true;
      sync();
      void recreate();
    };
    const onContextRestored = (): void => {
      // Still the current renderer: the new context could not be created at the loss, so it is created now.
      if (current?.app === app) void recreate();
    };
    canvas.addEventListener("webglcontextlost", onContextLost);
    canvas.addEventListener("webglcontextrestored", onContextRestored);

    return {
      app,
      view,
      dispose() {
        canvas.removeEventListener("webglcontextlost", onContextLost);
        canvas.removeEventListener("webglcontextrestored", onContextRestored);
        app.renderer.off("resize", layout);
        view.destroy();
        app.destroy({ removeView: true }, { children: true });
      },
    };
  };

  let recreating = false;

  /** A new renderer on a new canvas replaces the one whose context was lost (shaders compile on a fresh context). */
  const recreate = async (): Promise<void> => {
    if (recreating || isDestroyed()) return;
    recreating = true;
    try {
      const next = await createRenderer(true);
      if (isDestroyed()) {
        next.dispose();
        return;
      }
      const old = current;
      current = next;
      contextLost = false;
      old?.dispose();
      sync();
    } catch {
      // No WebGL context available yet (a real GPU reset): retried when the browser restores the lost one.
    } finally {
      recreating = false;
    }
  };

  ticker.add(() => {
    if (current === null) return;
    const started = performance.now();
    const frame = animator.advance(ticker.deltaMS);
    // A frozen scene (test hook) is the same picture on every tick: drawn twice, then the GPU rests.
    if (animator.isFrozen()) {
      if (stillFrames >= 2) return;
      stillFrames += 1;
    } else {
      stillFrames = 0;
    }
    current.view.draw(frame);
    current.app.renderer.render(current.app.stage);
    options.onFrame?.(performance.now() - started);
  });

  current = await createRenderer(false);

  const stopWatching = pauseWhenHidden(
    document,
    {
      start: () => {
        hidden = false;
        sync();
      },
      stop: () => {
        hidden = true;
        sync();
      },
    },
    animator,
  );
  sync();

  return {
    animator,
    destroy() {
      destroyed = true;
      stopWatching();
      ticker.destroy();
      current?.dispose();
      current = null;
    },
  };
}
