import { expect, test } from "@playwright/test";

import { openGame } from "../pipa/support/pipa";
import { freezeScene, waitForSceneHook, type FreezeState } from "./support/scene";

// SPEC-0011 E2E-01: beauty protected by visual regression (ADR-0021). The test hook freezes the scene clock and
// positions the scene in 6 keyframes; each capture (Chromium, 390×844) must match the reference image approved by the
// user, with at most 1% of the pixels different.
//
// Reference images: generated on the Linux runner of the CI `e2e` job (never on macOS) and stored next to this file in
// __screenshots__/keyframes.spec.ts/<name>-chromium-linux.png (snapshotPathTemplate of playwright.config.ts). Without
// them the test fails with "A snapshot doesn't exist"; the actual capture lands in artifacts/e2e (uploaded by the CI job
// on failure) for the user to approve. The HUD is hidden in the capture: only the canvas of the scene is compared.

/** Growth of the multiplier of SPEC-0008@2: m(t) = e^(k·t), k = 0.00006 per ms. */
const growthK = 0.00006;
const flightMsAt = (multiplier: number): number => Math.round(Math.log(multiplier) / growthK);

/** Stylesheet that hides every element but the scene canvas (HUD, history and panels change with the live round). */
const onlyTheScene = (specFile: string): string => specFile.replace(/keyframes\.spec\.ts$/u, "support/only-scene.css");

const keyframes: readonly { readonly name: string; readonly title: string; readonly atMs: number; readonly state: FreezeState }[] = [
  { name: "01-apostas", title: "apostas", atMs: 1_000, state: { phase: "betting", multiplier: 1 } },
  { name: "02-decolagem-300ms", title: "decolagem a 300 ms", atMs: 300, state: { phase: "flying", multiplier: 1.02 } },
  { name: "03-voo-2x", title: "voo a 2x", atMs: flightMsAt(2), state: { phase: "flying", multiplier: 2 } },
  {
    name: "04-voo-15x-por-do-sol",
    title: "voo a 15x, pôr do sol",
    atMs: flightMsAt(15),
    state: { phase: "flying", multiplier: 15 },
  },
  {
    name: "05-voo-120x-noite-lua",
    title: "voo a 120x, noite com lua",
    atMs: flightMsAt(120),
    state: { phase: "flying", multiplier: 120 },
  },
  { name: "06-crash-150ms", title: "crash a 150 ms", atMs: 150, state: { phase: "crashed", multiplier: 3.21 } },
];

/** Platform of the Playwright runner (the tsconfig of the E2E has no Node types). */
const platform = (globalThis as { process?: { platform?: string } }).process?.platform;

test.describe("SPEC-0011 quadros-chave da cena", () => {
  test.use({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
  // SPEC-0011 §7.6: the approved reference images exist only for the Linux runner of the CI (*-chromium-linux.png);
  // other systems render differently and have no reference to compare with.
  test.skip(
    platform !== "linux",
    `SPEC-0011 §7.6: as imagens de referência aprovadas são só do runner Linux da CI (plataforma atual: ${String(platform)})`,
  );

  for (const keyframe of keyframes) {
    test(`SPEC-0011:E2E-01 quadro-chave ${keyframe.title} igual à imagem de referência aprovada`, async ({ page }, testInfo) => {
      test.setTimeout(120_000);
      await openGame(page);
      await waitForSceneHook(page);
      const canvas = page.locator("canvas");
      await expect(canvas).toHaveCount(1);
      await expect(canvas).toBeVisible();

      await freezeScene(page, keyframe.atMs, keyframe.state);

      await expect(canvas).toHaveScreenshot(`${keyframe.name}.png`, {
        maxDiffPixelRatio: 0.01,
        animations: "disabled",
        caret: "hide",
        stylePath: onlyTheScene(testInfo.file),
        timeout: 20_000,
      });
    });
  }
});
