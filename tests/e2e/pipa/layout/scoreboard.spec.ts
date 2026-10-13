import { expect, test, type Page } from "@playwright/test";

import {
  openGame,
  roundTimeoutMs,
  testId,
  text,
  waitForState,
} from "../support/pipa";

// SPEC-0049 E2E-01: the multiplier block of the HUD (the number — `hud-countdown` while betting, `hud-multiplier` in
// flight and after the crash — together with the phase text) sits in the top third of the scene and never covers the
// kite, which the camera keeps near the centre of the canvas. In the betting, flying and crashed phases, measured
// against the scene canvas: the bottom edge of the block is above 35% of the scene height, the centre of the scene is
// not inside the block, the block takes at most 20% of the scene height and 40% of its width, and nothing from the
// texts up to the scene container paints an opaque background (alpha ≤ 0.3).
//
// "The block" is the nearest common ancestor of the number and the phase text; its box is the union of the boxes of
// the two texts and of that ancestor. Rounds are random: a round that crashes at 1,00x has no flight and the next one
// is followed, until each phase was measured.

type Phase = "betting" | "flying" | "crashed";

const phaseText: Record<Phase, string> = {
  betting: text.betting,
  flying: text.flying,
  crashed: text.crashed,
};

const numberTestId: Record<Phase, string> = {
  betting: testId.countdown,
  flying: testId.multiplier,
  crashed: testId.multiplier,
};

/** Rounds followed until the three phases were measured (a round may crash at 1,00x, with no flight). */
const maxRounds = 5;

interface Box {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

interface Measure {
  readonly scene: Box;
  readonly block: Box;
  /** Computed background colour of every element from the texts up to the scene container (exclusive). */
  readonly backgrounds: readonly {
    readonly element: string;
    readonly color: string;
  }[];
}

/** Measures the block in the page, only if the round is still in `phase` (null otherwise). */
async function measure(page: Page, phase: Phase): Promise<Measure | null> {
  return page.evaluate(
    ({ expectedPhase, numberId, phaseLabel }): Measure | null => {
      const hook = (
        window as unknown as {
          __APOGEU_TEST__?: { state(): { phase: string } };
        }
      ).__APOGEU_TEST__;
      if (hook?.state().phase !== expectedPhase) return null;

      const round = document.querySelector('section[aria-label="Rodada"]');
      const canvas = round?.querySelector("canvas") ?? null;
      const number =
        round?.querySelector(`[data-testid="${numberId}"]`) ?? null;
      const label =
        [...(round?.querySelectorAll("*") ?? [])].find(
          (element) =>
            element.children.length === 0 &&
            (element.textContent ?? "").trim() === phaseLabel,
        ) ?? null;
      if (
        round === null ||
        canvas === null ||
        number === null ||
        label === null
      ) {
        throw new Error(
          `scene or block missing in ${expectedPhase}: canvas=${String(canvas !== null)} ` +
            `number=${String(number !== null)} text=${String(label !== null)}`,
        );
      }

      // Nearest common ancestor of the number and the phase text.
      let common: Element | null = number;
      while (common !== null && !common.contains(label))
        common = common.parentElement;
      if (common === null)
        throw new Error("the number and the phase text share no ancestor");

      const box = (
        element: Element,
      ): { x: number; y: number; width: number; height: number } => {
        const rect = element.getBoundingClientRect();
        return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
      };
      const union = [number, label, common].map(box).reduce((a, b) => {
        const x = Math.min(a.x, b.x);
        const y = Math.min(a.y, b.y);
        return {
          x,
          y,
          width: Math.max(a.x + a.width, b.x + b.width) - x,
          height: Math.max(a.y + a.height, b.y + b.height) - y,
        };
      });

      // Everything between the texts and the element that holds the canvas (the scene container) must let the sky show.
      // The scene container: the nearest ancestor of the canvas that also holds the block.
      let sceneContainer: Element = canvas;
      while (
        !sceneContainer.contains(number) &&
        sceneContainer.parentElement !== null
      ) {
        sceneContainer = sceneContainer.parentElement;
      }
      const painted = new Set<Element>();
      for (const start of [number, label]) {
        for (
          let element: Element | null = start;
          element !== null && element !== sceneContainer;
          element = element.parentElement
        ) {
          painted.add(element);
        }
      }
      const backgrounds = [...painted].map((element) => ({
        element: `${element.tagName.toLowerCase()}.${[...element.classList].join(".")}`,
        color: getComputedStyle(element).backgroundColor,
      }));

      return { scene: box(canvas), block: union, backgrounds };
    },
    {
      expectedPhase: phase,
      numberId: numberTestId[phase],
      phaseLabel: phaseText[phase],
    },
  );
}

/** Alpha of a computed CSS colour: rgb()/rgba(), color()/oklab()/oklch() with "/ a", or transparent. */
function alphaOf(color: string): number {
  const value = color.trim().toLowerCase();
  if (value === "transparent") return 0;
  const slash = /\/\s*([\d.]+)(%?)\s*\)$/u.exec(value);
  if (slash !== null) return Number(slash[1]) / (slash[2] === "%" ? 100 : 1);
  const rgba = /^rgba\(([^)]*)\)$/u.exec(value);
  if (rgba !== null) {
    const parts = (rgba[1] ?? "").split(",");
    return parts.length === 4 ? Number(parts[3]) : 1;
  }
  return 1;
}

/** Soft checks: every violated rule of every phase is reported, not only the first. */
function checkLayout(
  phase: Phase,
  { scene, block, backgrounds }: Measure,
): void {
  const where = `${phase} (scene ${JSON.stringify(scene)}, block ${JSON.stringify(block)})`;
  expect.soft(scene.width, `scene canvas width in ${phase}`).toBeGreaterThan(0);
  expect
    .soft(scene.height, `scene canvas height in ${phase}`)
    .toBeGreaterThan(0);

  const bottom = (block.y + block.height - scene.y) / scene.height;
  expect
    .soft(
      bottom,
      `bottom edge of the block above 35% of the scene height — ${where}`,
    )
    .toBeLessThanOrEqual(0.35);

  const centerX = scene.x + scene.width / 2;
  const centerY = scene.y + scene.height / 2;
  const coversCenter =
    centerX >= block.x &&
    centerX <= block.x + block.width &&
    centerY >= block.y &&
    centerY <= block.y + block.height;
  expect
    .soft(
      coversCenter,
      `the centre of the scene (the kite) is not under the block — ${where}`,
    )
    .toBe(false);

  expect
    .soft(
      block.height / scene.height,
      `block height ≤ 20% of the scene — ${where}`,
    )
    .toBeLessThanOrEqual(0.2);
  expect
    .soft(
      block.width / scene.width,
      `block width ≤ 40% of the scene — ${where}`,
    )
    .toBeLessThanOrEqual(0.4);

  const opaque = backgrounds.filter((entry) => alphaOf(entry.color) > 0.3);
  expect
    .soft(opaque, `no opaque background behind the block in ${phase}`)
    .toEqual([]);
}

test("SPEC-0049:E2E-01 o placar fica no topo da cena, compacto e sem fundo opaco, sem cobrir a pipa (apostas, voo e crash)", async ({
  page,
}) => {
  test.setTimeout((maxRounds + 1) * roundTimeoutMs);
  await openGame(page);
  await expect(page.locator('section[aria-label="Rodada"] canvas')).toBeVisible(
    { timeout: 30_000 },
  );

  const pending = new Set<Phase>(["betting", "flying", "crashed"]);
  // The phase on screen when the game opens is measured too.
  let previous: { readonly phase: string; readonly roundId: string | null } = {
    phase: "",
    roundId: null,
  };
  const deadline = Date.now() + maxRounds * roundTimeoutMs;

  while (pending.size > 0) {
    if (Date.now() > deadline) {
      throw new Error(
        `phases never measured in ${String(maxRounds)} rounds: ${[...pending].join(", ")}`,
      );
    }
    const seen = previous;
    const current = await waitForState(
      page,
      (state) =>
        pending.has(state.phase as Phase) &&
        (state.phase !== seen.phase || state.roundId !== seen.roundId),
      `one of the phases ${[...pending].join(", ")}`,
    );
    const phase = current.phase as Phase;
    const measured = await measure(page, phase);
    previous = current;
    if (measured === null) continue;
    checkLayout(phase, measured);
    pending.delete(phase);
  }
});
