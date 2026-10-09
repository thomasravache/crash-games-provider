// The sky as a single texture (SPEC-0011 §2, ADR-0019): a 1×256 vertical gradient blended on the CPU from the day,
// sunset and night ramps and stretched over the scene by one sprite. One full-screen quad per frame instead of one per
// sky layer — fill rate is what a software renderer (and a mid-range phone) runs out of first.

import { Texture } from "pixi.js";

/** Colour stops (top → horizon) of the three skies, `#rrggbb`. */
export interface SkyGradients {
  readonly day: readonly string[];
  readonly sunset: readonly string[];
  readonly night: readonly string[];
}

const texels = 256;
/** Steps of the sky ramp (0..2) the texture is repainted at; finer changes are invisible. */
const progressSteps = 400;

export interface SkyTexture {
  readonly texture: Texture;
  /** Paints the sky at `progress` (0 = day, 1 = sunset, 2 = night). */
  paint(progress: number): void;
  destroy(): void;
}

/** The gradient of `stops` (top → horizon), sampled at `texels` points, RGBA. */
function sample(stops: readonly string[]): Uint8ClampedArray {
  const canvas = document.createElement("canvas");
  canvas.width = 1;
  canvas.height = texels;
  const context = canvas.getContext("2d");
  if (context === null) return new Uint8ClampedArray(texels * 4);
  const gradient = context.createLinearGradient(0, 0, 0, texels);
  stops.forEach((color, index) => {
    gradient.addColorStop(index / Math.max(1, stops.length - 1), color);
  });
  context.fillStyle = gradient;
  context.fillRect(0, 0, 1, texels);
  return context.getImageData(0, 0, 1, texels).data;
}

export function createSkyTexture(gradients: SkyGradients): SkyTexture {
  const ramps = [sample(gradients.day), sample(gradients.sunset), sample(gradients.night)];
  const canvas = document.createElement("canvas");
  canvas.width = 1;
  canvas.height = texels;
  const context = canvas.getContext("2d");
  const image = context?.createImageData(1, texels) ?? null;
  const texture = Texture.from(canvas);
  let painted = -1;

  return {
    texture,
    paint(progress) {
      const step = Math.round(Math.min(2, Math.max(0, progress)) * progressSteps);
      if (step === painted || context === null || image === null) return;
      painted = step;
      const level = step / progressSteps;
      const lower = Math.min(1, Math.floor(level));
      const t = level - lower;
      const from = ramps[lower];
      const to = ramps[lower + 1];
      if (from === undefined || to === undefined) return;
      for (let index = 0; index < texels * 4; index += 1) {
        const a = from[index] ?? 0;
        image.data[index] = a + ((to[index] ?? a) - a) * t;
      }
      context.putImageData(image, 0, 0);
      texture.source.update();
    },
    destroy() {
      texture.destroy(true);
    },
  };
}
