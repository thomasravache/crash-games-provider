import { describe, expect, test } from "vitest";

import { bettingWindGain, tensionHz, windGain } from "./support/audio-module";

// SPEC-0018 UT-01: the mix grows with the risk. For m = 1, 2, 5, 10, 25, 50 and 100, windGain stays in [0.15, 0.60]
// and tensionHz in [220, 880] Hz, and neither ever decreases as m grows (§6 formulas).

const multipliers = [1, 2, 5, 10, 25, 50, 100];

const nonDecreasing = (values: readonly number[]): boolean =>
  values.every((value, index) => index === 0 || value >= (values[index - 1] ?? -Infinity));

describe("SPEC-0018 mistura do som", () => {
  test("SPEC-0018:UT-01 windGain fica em [0,15; 0,60] e nunca diminui com m crescente", () => {
    const gains = multipliers.map((m) => windGain(m));

    for (const gain of gains) {
      expect(gain).toBeGreaterThanOrEqual(0.15);
      expect(gain).toBeLessThanOrEqual(0.6);
    }
    expect(nonDecreasing(gains), `windGain over ${multipliers.join(", ")}: ${gains.join(", ")}`).toBe(true);
    // windGain(m) = 0.15 + 0.45 · min(1, log10(m)/2); the betting wind is 0.15.
    expect(windGain(1)).toBeCloseTo(bettingWindGain, 10);
    expect(windGain(10)).toBeCloseTo(0.375, 10);
    expect(windGain(100)).toBeCloseTo(0.6, 10);
    expect(windGain(1_000)).toBeCloseTo(0.6, 10);
  });

  test("SPEC-0018:UT-01 tensionHz fica em [220; 880] Hz e nunca diminui com m crescente", () => {
    const pitches = multipliers.map((m) => tensionHz(m));

    for (const hz of pitches) {
      expect(hz).toBeGreaterThanOrEqual(220);
      expect(hz).toBeLessThanOrEqual(880);
    }
    expect(nonDecreasing(pitches), `tensionHz over ${multipliers.join(", ")}: ${pitches.join(", ")}`).toBe(true);
    // tensionHz(m) = 220 · 2^(min(log2(m), 6)/3): one octave every 3 doublings, capped at 64x.
    expect(tensionHz(1)).toBeCloseTo(220, 6);
    expect(tensionHz(8)).toBeCloseTo(440, 6);
    expect(tensionHz(64)).toBeCloseTo(880, 6);
    expect(tensionHz(100)).toBeCloseTo(880, 6);
    expect(tensionHz(10_000)).toBeCloseTo(880, 6);
  });
});
