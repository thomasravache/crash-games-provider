import { expect, test, type Page, type Response } from "@playwright/test";

import {
  demoSiteUrl,
  enterCasino,
  expectNoAxeViolations,
  gamePage,
  gameSandbox,
  lobby,
  pipaAbout,
  pipaHud,
} from "./support/demo-site";

// SPEC-0045 E2E-01: the logged-in player opens the Pipa from the lobby of the demo casino; the casino BFF launches it at
// the Apogeu (POST /casino/play/pipa, signed on the server) and the game page (/jogo/pipa, Stitch "Cassino Demo — Pipa
// (Refinado)") shows the game in an iframe with the minimum sandbox of §6, the fullscreen button, "← Voltar ao lobby" and
// the "Sobre o jogo" column. The player sees a round in the iframe, goes back to the lobby and opens the Pipa again: a
// new launch (a different launchUrl) and the game loads again. The frame of the casino passes axe (WCAG 2.2 AA).

const playPath = "/casino/play/pipa";
const gamePath = "/jogo/pipa";

/** Generous: the game loads in a cross-origin iframe, connects to the round hub and shows a phase, twice. */
const gameLoadTimeoutMs = 30_000;

function isPlay(response: Response): boolean {
  return (
    response.request().method() === "POST" &&
    new URL(response.url()).pathname === playPath
  );
}

/** Reads the launchUrl of a 200 response of POST /casino/play/pipa (SPEC-0045 §6). */
async function launchUrlOf(response: Response): Promise<string> {
  expect(response.status(), `POST ${playPath}`).toBe(200);
  const body: unknown = await response.json();
  const launchUrl =
    typeof body === "object" && body !== null
      ? (body as Record<string, unknown>)["launchUrl"]
      : undefined;
  if (typeof launchUrl !== "string") {
    throw new Error(
      `the play response must carry launchUrl: ${JSON.stringify(body)}`,
    );
  }
  return launchUrl;
}

/** The Pipa card of the lobby: the control that opens the game (a link or a button named after the Pipa). */
async function openPipaFromLobby(page: Page): Promise<string> {
  const panel = page.getByRole("tabpanel");
  await expect(panel.getByRole("heading", { name: lobby.pipa })).toBeVisible();
  const open = panel
    .getByRole("link", { name: lobby.pipa })
    .or(panel.getByRole("button", { name: lobby.pipa }))
    .first();
  await expect(open, "the Pipa card of the lobby opens the game").toBeVisible();
  const played = page.waitForResponse(isPlay);
  await open.click();
  return launchUrlOf(await played);
}

/** The game page with the iframe of the launch, and the Pipa loaded in it (DEMO badge and a round phase). */
async function expectGameLoaded(page: Page, launchUrl: string): Promise<void> {
  await expect(page).toHaveURL(new RegExp(`${gamePath}$`, "u"));
  const frame = page.locator("iframe");
  await expect(frame).toHaveCount(1);
  await expect(frame).toHaveAttribute("src", launchUrl);
  await expect(frame).toHaveAttribute("sandbox", gameSandbox);

  const game = page.frameLocator("iframe");
  await expect(game.getByText(pipaHud.demoBadge, { exact: true })).toBeVisible({
    timeout: gameLoadTimeoutMs,
  });
  // A round of the Pipa in the iframe: one of the phases of SPEC-0010 §6 on screen.
  await expect(game.getByText(pipaHud.phase).first()).toBeVisible({
    timeout: gameLoadTimeoutMs,
  });
}

test("SPEC-0045:E2E-01 o jogador abre a Pipa no lobby, vê uma rodada no iframe, volta ao lobby e reabre com um lançamento novo", async ({
  page,
}) => {
  test.setTimeout(180_000);
  const operatorCalls: string[] = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname.startsWith("/api/v1/operator/")) {
      operatorCalls.push(request.url());
    }
  });

  await enterCasino(page);

  // First launch: from the lobby to the game page, the Pipa loaded in the iframe.
  const firstLaunch = await openPipaFromLobby(page);
  await expectGameLoaded(page, firstLaunch);

  // The frame of the casino (§6): fullscreen, back to the lobby and "Sobre o jogo".
  await expect(
    page.getByRole("button", { name: gamePage.fullscreen }),
  ).toBeVisible();
  const back = page
    .getByRole("link", { name: gamePage.back })
    .or(page.getByRole("button", { name: gamePage.back }))
    .first();
  await expect(back).toBeVisible();
  await expect(
    page.getByRole("heading", { name: gamePage.about }),
  ).toBeVisible();
  // "Sobre o jogo" of the Pipa: fixed data of the demo-site (Emenda v1); page locators never reach into the iframe.
  for (const fact of Object.values(pipaAbout)) {
    await expect(page.getByText(fact).first()).toBeVisible();
  }
  await expectNoAxeViolations(page, "game", { frames: false });

  // Back to the lobby.
  await back.click();
  await expect(page).not.toHaveURL(new RegExp(`${gamePath}$`, "u"));
  await expect(
    page.getByRole("tablist", { name: lobby.categories }),
  ).toBeVisible();
  await expect(page.locator("iframe")).toHaveCount(0);

  // Reopening is a new launch: a different single-use launchUrl, and the game loads again.
  const secondLaunch = await openPipaFromLobby(page);
  expect(secondLaunch, "reopening the Pipa launches it again").not.toBe(
    firstLaunch,
  );
  await expectGameLoaded(page, secondLaunch);

  // §4/§5: the launch is signed by the casino server; the browser never calls the operator API of the Apogeu.
  expect(operatorCalls).toEqual([]);
  expect(new URL(page.url()).origin).toBe(new URL(demoSiteUrl()).origin);
});
