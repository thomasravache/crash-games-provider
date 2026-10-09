import { expect, test } from "@playwright/test";

import {
  controllableNetwork,
  hookState,
  newLaunchUrl,
  openGame,
  roundTimeoutMs,
  serverCrashPoint,
  text,
  waitForCrash,
  waitForState,
} from "./support/pipa";

// SPEC-0010 E2E-04: sessão inválida (lt já usado) e queda de rede de 5 s com o selo "Reconectando…" e a ressincronização.

/** How long the network stays down (SPEC-0010 E2E-04). */
const outageMs = 5_000;

test("SPEC-0010:E2E-04 um lt já usado mostra a tela de sessão expirada", async ({ page, request }) => {
  const launchUrl = await newLaunchUrl(request);

  await page.goto(launchUrl);
  await expect(page.getByText(text.demoBadge, { exact: true })).toBeVisible({ timeout: 30_000 });

  const again = await page.context().newPage();
  await again.goto(launchUrl);
  await expect(again.getByText(text.sessionExpired, { exact: true })).toBeVisible({ timeout: 30_000 });
  await expect(again.getByText(text.demoBadge, { exact: true })).toBeHidden();
});

test("SPEC-0010:E2E-04 com a rede derrubada por 5 s aparece Reconectando… e o estado volta a acompanhar a rodada", async ({
  page,
  request,
}) => {
  test.setTimeout(2 * roundTimeoutMs + 120_000);
  const network = await controllableNetwork(page);
  await openGame(page);
  await waitForState(page, (state) => state.connection === "connected" && state.phase !== "idle", "the first snapshot");

  await network.down();
  await expect(page.getByText(text.reconnecting, { exact: true })).toBeVisible({ timeout: outageMs });
  expect((await hookState(page)).connection).toBe("reconnecting");
  // The outage itself lasts 5 s (the scenario, not a synchronization wait).
  await page.waitForTimeout(outageMs);
  await network.up();

  await expect(page.getByText(text.reconnecting, { exact: true })).toBeHidden({ timeout: 60_000 });
  const resynced = await waitForState(page, (state) => state.connection === "connected", "connection back", 60_000);

  // Following the rounds again: the next crash is the server's.
  const crashed = await waitForCrash(page, resynced);
  const roundId = crashed.roundId ?? "";
  expect(crashed.crashPoint).toBe(await serverCrashPoint(request, roundId));
  await expect(page.getByText(text.crashed, { exact: true })).toBeVisible();
});
