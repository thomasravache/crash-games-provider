import { expect, test } from "@playwright/test";

import {
  expectNoAxeViolations,
  hookState,
  hudMultiplier,
  openGame,
  roundTimeoutMs,
  serverCrashPoint,
  testId,
  text,
  waitForState,
  type HookRoundState,
} from "./support/pipa";

// SPEC-0010 E2E-02: uma rodada completa no HUD, conferida com o estado (gancho de teste) e com o servidor; painel de
// aposta desabilitado; axe sem violações em cada estado do HUD (§7.6).

/** Rounds tried until one flies long enough for the multiplier to visibly grow (a crash at 1.00x shows no flight). */
const maxRounds = 3;

test("SPEC-0010:E2E-02 rodada completa: PREPARE SUA PIPA, EM VOO crescente e PIPA CORTADA! com o crash do servidor", async ({
  page,
  request,
}) => {
  test.setTimeout(maxRounds * roundTimeoutMs + 60_000);
  await openGame(page);

  // The disabled betting placeholder of SPEC-0010 was replaced by the betting panel of SPEC-0026: both panels are
  // present in every phase (their behaviour is covered by tests/e2e/pipa/bets/**).
  const bet = page.getByTestId("bet-panel-1");
  await expect(bet).toBeVisible();
  await expect(page.getByTestId("bet-panel-2")).toBeVisible();

  let followed: HookRoundState | undefined;
  for (
    let attempt = 0;
    attempt < maxRounds && followed === undefined;
    attempt += 1
  ) {
    const previous = await hookState(page);
    const betting = await waitForState(
      page,
      (state) =>
        state.phase === "betting" &&
        state.roundId !== null &&
        state.roundId !== previous.roundId,
      "a new round in betting",
    );
    await expect(page.getByText(text.betting, { exact: true })).toBeVisible();
    await expect(page.getByTestId(testId.countdown)).toBeVisible();
    await expect(bet).toBeVisible();
    await expectNoAxeViolations(page, "betting");

    const flying = await waitForState(
      page,
      (state) => state.roundId === betting.roundId && state.phase !== "betting",
      `round ${String(betting.roundNumber)} leaving betting`,
    );
    if (flying.phase !== "flying") continue;
    await expect(page.getByText(text.flying, { exact: true })).toBeVisible();
    const first = await hudMultiplier(page);
    await expectNoAxeViolations(page, "flying");

    // Growing multiplier, unless this round crashes before the next tick.
    let grew = false;
    await expect
      .poll(
        async () => {
          if ((await hudMultiplier(page)) > first) grew = true;
          return grew || (await hookState(page)).phase !== "flying";
        },
        {
          message: "the multiplier grows while flying",
          timeout: roundTimeoutMs,
        },
      )
      .toBe(true);
    if (grew) followed = betting;
  }
  if (followed === undefined)
    throw new Error(`no round flew long enough in ${String(maxRounds)} rounds`);
  await expect(bet).toBeVisible();

  const roundId = followed.roundId ?? "";
  const crashed = await waitForState(
    page,
    (state) =>
      state.roundId === roundId &&
      (state.phase === "crashed" || state.phase === "annulled"),
    `the crash of round ${roundId}`,
  );
  expect(crashed.phase).toBe("crashed");
  await expect(page.getByText(text.crashed, { exact: true })).toBeVisible();

  // The crash shown is the server's: hook state, HUD and the persisted round agree.
  const fromServer = await serverCrashPoint(request, roundId);
  expect(crashed.crashPoint).toBe(fromServer);
  expect(await hudMultiplier(page)).toBe(Number(fromServer));

  // The history gains the round.
  await expect
    .poll(async () => (await hookState(page)).history[0]?.roundId)
    .toBe(roundId);
  const latest = page
    .getByRole("list", { name: text.historyLabel })
    .getByRole("listitem")
    .first();
  await expect(latest).toBeVisible();
  expect(Number((await latest.innerText()).replace(/\D/gu, "")) / 100).toBe(
    Number(fromServer),
  );

  await expect(bet).toBeVisible();
  await expectNoAxeViolations(page, "crashed");
});
