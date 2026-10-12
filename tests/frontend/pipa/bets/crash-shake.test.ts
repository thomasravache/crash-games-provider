import { readFileSync } from "node:fs";

import {
  crashShakeClassName,
  crashShakeDurationMs,
  Hud,
  hudText,
  prefersReducedMotion,
  reducedMotionQuery,
  useCrashShake,
  type HudProps,
} from "@apogeu/game-ui";
import {
  initialRoundState,
  type RoundPhase,
  type RoundState,
} from "@apogeu/game-sdk/state";
import { afterEach, describe, expect, test, vi } from "vitest";

import { render } from "./support/react";

// SPEC-0026 UT-07: on RoundCrashed the "PIPA CORTADA!" text of the HUD shakes for at most 300 ms; with
// `prefers-reduced-motion: reduce` it does not shake (ADR-0021). The shake is the class `hud-crash-shake` (keyframes in
// hud-base.css), applied from the first paint of the crash (no effect delay); the preference comes from matchMedia.

const hudCss = readFileSync(
  new URL(
    "../../../../src/frontend/packages/game-ui/src/hud-base.css",
    import.meta.url,
  ),
  "utf8",
);

const media = (
  reduce: boolean,
): { matchMedia(query: string): { matches: boolean } } => ({
  matchMedia: (query) => ({ matches: reduce && query === reducedMotionQuery }),
});

function round(phase: RoundPhase): RoundState {
  return {
    ...initialRoundState,
    phase,
    roundId: "round-41",
    roundNumber: 41,
    multiplier: phase === "crashed" ? 2.47 : 1.5,
    crashPoint: phase === "crashed" ? "2.47" : null,
    connection: "connected",
  };
}

function hudMarkup(phase: RoundPhase): string {
  const props: HudProps = {
    state: round(phase),
    nickname: "Jogador Demo",
    demoBalance: "R$ 1.000,00",
    onOpenFairness: () => undefined,
  };
  return render(Hud, props);
}

/** Class attribute of the element whose own text is "PIPA CORTADA!", or null when the text is not shown. */
function crashTextClass(markup: string): string | null {
  const match = new RegExp(
    `<([a-z]+)\\b([^>]*)>${hudText.crashed}</\\1>`,
    "u",
  ).exec(markup);
  if (match === null) return null;
  return /class="([^"]*)"/u.exec(match[2] ?? "")?.[1] ?? "";
}

/** Milliseconds of every time value in a CSS declaration ("0.3s", "250ms"). */
function times(declaration: string): number[] {
  return [...declaration.matchAll(/(\d*\.?\d+)(ms|s)\b/gu)].map((found) =>
    found[2] === "s" ? Number(found[1]) * 1000 : Number(found[1]),
  );
}

function Probe({
  phase,
  roundId,
  reducedMotion,
}: {
  readonly phase: RoundPhase;
  readonly roundId: string | null;
  readonly reducedMotion: boolean;
}): string {
  return String(useCrashShake(phase, roundId, reducedMotion));
}

describe("SPEC-0026 tremor do texto PIPA CORTADA!", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  test("SPEC-0026:UT-07 prefersReducedMotion lê (prefers-reduced-motion: reduce) e é false sem matchMedia", () => {
    expect(reducedMotionQuery).toBe("(prefers-reduced-motion: reduce)");
    expect(prefersReducedMotion(media(true))).toBe(true);
    expect(prefersReducedMotion(media(false))).toBe(false);
    expect(prefersReducedMotion(undefined)).toBe(false);
  });

  test("SPEC-0026:UT-07 o tremor dura no máximo 300 ms (constante e keyframes do hud-base.css)", () => {
    expect(crashShakeDurationMs).toBeGreaterThan(0);
    expect(crashShakeDurationMs).toBeLessThanOrEqual(300);

    const rule = new RegExp(
      `\\.${crashShakeClassName}\\s*\\{([^}]*)\\}`,
      "u",
    ).exec(hudCss);
    expect(
      rule,
      `hud-base.css must define .${crashShakeClassName}`,
    ).not.toBeNull();
    const animation = (rule?.[1] ?? "")
      .split(";")
      .filter((declaration) =>
        /^\s*animation(-duration)?\s*:/u.test(declaration),
      )
      .join(";");
    expect(
      animation,
      `.${crashShakeClassName} must declare an animation`,
    ).not.toBe("");
    const durations = times(animation);
    expect(durations.length).toBeGreaterThan(0);
    // The first time of the shorthand is the duration; a delay would add to it, so every time counts.
    expect(
      durations.reduce((sum, value) => sum + value, 0),
    ).toBeLessThanOrEqual(300);
    // A single run: the shake never repeats.
    expect(animation).not.toMatch(/infinite/u);
  });

  test("SPEC-0026:UT-07 useCrashShake: treme na rodada que entrou em crashed; nunca com movimento reduzido nem fora do crash", () => {
    const shake = (phase: RoundPhase, reducedMotion: boolean): string =>
      render(Probe, { phase, roundId: "round-41", reducedMotion });

    expect(shake("crashed", false)).toBe("true");
    expect(shake("crashed", true)).toBe("false");
    for (const phase of ["idle", "betting", "flying", "annulled"] as const) {
      expect(shake(phase, false), phase).toBe("false");
    }
  });

  test("SPEC-0026:UT-07 HUD em crash: PIPA CORTADA! treme; com prefers-reduced-motion: reduce aparece sem tremor", () => {
    vi.stubGlobal("window", media(false));
    vi.stubGlobal("matchMedia", media(false).matchMedia);
    expect(crashTextClass(hudMarkup("crashed"))?.split(/\s+/u)).toContain(
      crashShakeClassName,
    );
    // In flight there is no crash text, and nothing shakes.
    const flying = hudMarkup("flying");
    expect(crashTextClass(flying)).toBeNull();
    expect(flying).not.toContain(crashShakeClassName);

    vi.stubGlobal("window", media(true));
    vi.stubGlobal("matchMedia", media(true).matchMedia);
    const reduced = hudMarkup("crashed");
    expect(crashTextClass(reduced)).not.toBeNull();
    expect(reduced).not.toContain(crashShakeClassName);
  });
});
