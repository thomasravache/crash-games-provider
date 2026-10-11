import { describe, expect, test } from "vitest";

import { FakeDocument, flyTo, loggedSince, names, newAudioRig } from "./support/audio";

// SPEC-0018 UT-07: hidden tab (ADR-0021, ADR-0024). visibilitychange to hidden during the flight suspends the
// AudioContext; visible again 10 s later, already in another round, it resumes in the current state without playing
// the milestones, crash or takeoff it missed.

describe("SPEC-0018 aba oculta", () => {
  test("SPEC-0018:UT-07 oculta no voo e visível 10 s depois em outra rodada: suspende e retoma no estado atual sem sinais perdidos", async () => {
    const document = new FakeDocument();
    const rig = newAudioRig({ visibility: document });
    await rig.audio.unlock();
    rig.audio.prepare();
    rig.advance(1_000);
    rig.audio.takeoff(0);
    rig.advance(700);
    flyTo(rig, 1, 3);
    const context = rig.context();
    expect(names(rig.audio.cueLog())).toContain("milestone");

    document.become("hidden");
    await Promise.resolve();
    expect(context.calls.map((call) => call.name).at(-1)).toBe("suspend");
    expect(context.state).toBe("suspended");
    expect(rig.audio.state()).toBe("suspended");
    const logged = rig.audio.cueLog().length;
    const started = context.started.length;

    // 10 s hidden: the round crashes, the next one bets, takes off and crosses 2x. The binding keeps calling the audio.
    rig.audio.crash(3.4);
    rig.advance(2_000);
    rig.audio.prepare();
    rig.advance(6_000);
    rig.audio.takeoff(0);
    flyTo(rig, 1, 2.5);
    expect(rig.audio.cueLog().length, "nothing logged while hidden").toBe(logged);
    expect(context.started.length, "no source started while hidden").toBe(started);

    document.become("visible");
    await Promise.resolve();
    expect(context.calls.map((call) => call.name).at(-1)).toBe("resume");
    expect(context.state).toBe("running");
    expect(rig.audio.state()).toBe("running");
    rig.advance(300);

    const back = names(loggedSince(rig.audio, logged));
    // Only the voices of the current state (flight at 2.5x): no missed snap, fall, restart, takeoff nor 2x milestone.
    expect(back.filter((name) => name !== "wind" && name !== "tension")).toEqual([]);
    expect(context.output(rig.nowMs() / 1000), "the current flight is heard").toBeGreaterThan(0);

    // The flight goes on: 5x sounds; 2x, missed while hidden, never does in this round.
    flyTo(rig, 2.5, 5.5);
    const milestones = loggedSince(rig.audio, logged)
      .filter((entry) => entry.name === "milestone")
      .map((entry) => entry.milestone);
    expect(milestones).toEqual([5]);
  });

  test("SPEC-0018:UT-07 aba oculta antes do primeiro gesto: nada é criado; o áudio continua bloqueado", () => {
    const document = new FakeDocument();
    const rig = newAudioRig({ visibility: document });

    document.become("hidden");
    document.become("visible");

    expect(rig.contexts).toHaveLength(0);
    expect(rig.audio.state()).toBe("locked");
  });
});
