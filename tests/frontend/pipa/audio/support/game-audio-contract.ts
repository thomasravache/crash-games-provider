// Contract suite of GameAudio (SPEC-0018 §6), run against any implementation (SPEC-0018 CT-01 runs it against
// PipaAudio). The implementation is driven by the same state sequences of SPEC-0010@1 as the GameAnimator contract suite
// (tests/frontend/pipa/scene/support/game-animator-contract.ts) — directly and through bindGameAnimator with the round
// store, since GameAudio has the GameAnimator calls — and must accept every call, never throw, and log the round
// script of §6 in its cueLog: wind (betting) → takeoff → milestones → snap → fall → silence → restart (next betting).
import { createRoundStore } from "@apogeu/game-sdk/state";
import { bindGameAnimator } from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

import {
  bettingEvent,
  crashedEvent,
  flyingEvent,
  roundIds,
  snapshotEvent,
  syncEvent,
} from "../../support/rounds";
import { isSubsequence, names } from "./audio";
import type { AudioTestHook, CueEntry, GameAudio } from "./audio-module";

export interface AudioUnderContract {
  readonly audio: GameAudio & AudioTestHook;
  /** Lets `ms` of audio time pass. */
  advance(ms: number): void;
  /** How many AudioContexts the implementation created so far. */
  contextsCreated(): number;
}

const t0 = Date.parse("2026-10-09T12:00:00.000Z");

const count = (log: readonly CueEntry[], name: CueEntry["name"]): number =>
  log.filter((entry) => entry.name === name).length;

const milestonesOf = (log: readonly CueEntry[]): (number | undefined)[] =>
  log.filter((entry) => entry.name === "milestone").map((entry) => entry.milestone);

export function describeGameAudioContract(tag: string, create: () => AudioUnderContract): void {
  describe(`${tag} contrato do GameAudio (sequências da SPEC-0010@1)`, () => {
    test(`${tag} antes de unlock() toda a sequência é aceita sem som: nenhum contexto, cueLog vazio`, () => {
      const { audio, advance, contextsCreated } = create();

      audio.prepare();
      advance(1_000);
      audio.takeoff(t0);
      for (const multiplier of [1.06, 1.34, 2.45]) {
        audio.setMultiplier(multiplier);
        advance(16);
      }
      audio.crash(2.78);
      audio.reset();

      expect(contextsCreated()).toBe(0);
      expect(audio.cueLog()).toEqual([]);
      expect(audio.state()).toBe("locked");
    });

    test(`${tag} sequência canônica: prepare → takeoff → setMultiplier → crash → prepare segue o roteiro`, async () => {
      const { audio, advance } = create();
      await audio.unlock();

      audio.prepare();
      advance(1_000);
      audio.takeoff(t0);
      advance(700);
      for (const multiplier of [1.06, 1.34, 2.45]) {
        audio.setMultiplier(multiplier);
        advance(16);
      }
      audio.crash(2.78);
      advance(16);
      // A late tick after the crash does not bring the flight back.
      audio.setMultiplier(2.9);
      advance(2_000);
      const crashed = [...audio.cueLog()];
      audio.prepare();
      advance(1_000);

      const log = audio.cueLog();
      expect(
        isSubsequence(names(log), ["wind", "takeoff", "milestone", "snap", "fall", "silence", "restart"]),
        `cue log ${names(log).join(" → ")}`,
      ).toBe(true);
      expect(milestonesOf(log)).toEqual([2]);
      expect(count(log, "takeoff")).toBe(1);
      expect(count(log, "snap")).toBe(1);
      // After the crash script, only the restart of the next betting (and its wind) is played.
      expect(names(log.slice(crashed.length)).filter((name) => name !== "restart" && name !== "wind")).toEqual([]);
      expect(names(crashed).slice(names(crashed).indexOf("snap"))).toEqual(["snap", "fall", "silence"]);
      expect(audio.state()).toBe("running");
    });

    test(`${tag} rodadas pelo adaptador bindGameAnimator: Betting → Flying → Crashed → Betting → anulada`, async () => {
      const { audio, advance } = create();
      await audio.unlock();
      const store = createRoundStore();
      store.dispatch(snapshotEvent(null));
      const unbind = bindGameAnimator(store, audio);

      store.dispatch(bettingEvent(7, t0));
      advance(1_000);
      store.dispatch(flyingEvent(7, t0));
      // e^(0.00006·15000) ≈ 2.46x: the 2x milestone.
      for (const elapsedMs of [1_000, 5_000, 12_000, 15_000]) {
        store.tick(t0 + elapsedMs);
        advance(16);
      }
      advance(700);
      store.dispatch(crashedEvent(7, "2.78", t0 + 17_040));
      advance(2_000);
      store.dispatch(bettingEvent(8, t0 + 30_000));
      advance(1_000);
      const beforeAnnulled = audio.cueLog().length;
      store.dispatch({ type: "annulled", roundId: roundIds(8).roundId, reason: "test" });
      advance(1_000);

      const log = audio.cueLog();
      expect(
        isSubsequence(names(log), ["wind", "takeoff", "milestone", "snap", "fall", "silence", "restart"]),
        `cue log ${names(log).join(" → ")}`,
      ).toBe(true);
      expect(milestonesOf(log)).toEqual([2]);
      expect(count(log, "snap")).toBe(1);
      // An annulled round plays no crash.
      expect(names(log.slice(beforeAnnulled)).filter((name) => ["snap", "fall", "takeoff"].includes(name))).toEqual(
        [],
      );
      unbind();
    });

    test(`${tag} ressincronização: entrada no meio do voo, multiplicador para trás, crash sem decolagem e prepare repetido`, async () => {
      const { audio, advance } = create();
      await audio.unlock();

      // Joined mid-flight (snapshot of a flying round): takeoff and the current multiplier, without prepare.
      audio.takeoff(t0 - 40_000);
      audio.setMultiplier(11);
      advance(700);
      // A sync takes the interpolated multiplier slightly back (12x → 11.8x) and forward again.
      audio.setMultiplier(12);
      advance(16);
      audio.setMultiplier(11.8);
      advance(16);
      // A takeoff repeated by a reconnection keeps the flight.
      audio.takeoff(t0 - 40_000);
      audio.setMultiplier(12.1);
      advance(16);
      const flying = [...audio.cueLog()];
      expect(names(flying)).toContain("tension");
      expect(count(flying, "takeoff"), "a repeated takeoff does not pull again").toBeLessThanOrEqual(1);
      expect(count(flying, "milestone") - new Set(milestonesOf(flying)).size, "no milestone twice").toBe(0);

      audio.prepare();
      audio.prepare();
      advance(1_000);
      // Joined at a crashed round: crash without a takeoff, then repeated resets.
      audio.crash(4.5);
      advance(2_000);
      audio.reset();
      audio.reset();
      advance(1_000);

      const log = audio.cueLog();
      expect(count(log, "snap")).toBe(1);
      expect(audio.state()).toBe("running");
    });

    test(`${tag} estado de sync do servidor durante o voo é seguido sem exceção`, async () => {
      const { audio, advance } = create();
      await audio.unlock();
      const store = createRoundStore();
      store.dispatch(snapshotEvent(null));
      const unbind = bindGameAnimator(store, audio);

      store.dispatch(bettingEvent(9, t0));
      store.dispatch(flyingEvent(9, t0));
      store.dispatch(syncEvent(9, t0 + 5_000, "1.35"));
      advance(700);

      expect(names(audio.cueLog())).toContain("tension");
      unbind();
    });

    test(`${tag} controles e dispose() são aceitos em qualquer fase, sem exceção`, async () => {
      const { audio, advance } = create();
      await audio.unlock();
      audio.prepare();
      audio.setVolume(0);
      audio.setVolume(1);
      audio.setMuted(true);
      audio.takeoff(t0);
      audio.setMultiplier(1.5);
      audio.setMuted(false);
      advance(100);
      audio.dispose();

      expect(() => {
        audio.setMultiplier(2);
        audio.crash(2.1);
        audio.prepare();
        audio.reset();
      }).not.toThrow();
    });
  });
}
