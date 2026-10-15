import { expect, test, type Page, type Response } from "@playwright/test";

import {
  enterCasino,
  expectNoAxeViolations,
  lobby,
  pipaKit,
} from "./support/demo-site";

// SPEC-0044 E2E-01: the logged-in player opens the lobby of the demo casino (Stitch "Cassino Demo — Lobby") and sees the
// Pipa banner (the featured game, with the banner art of the kit), the category tabs with Crash, and the Pipa card with
// its thumbnail from the kit (served by the game front, §6 Emenda v1). The games come from the BFF (GET /casino/games),
// never from the Apogeu directly (§5). The lobby passes axe (WCAG 2.2 AA).

function isPath(path: string): (response: Response) => boolean {
  return (response) => new URL(response.url()).pathname === path;
}

/** Whether an image of the page named like the Pipa shows the given kit art, fully loaded. */
async function pipaImageLoaded(page: Page, artPath: string): Promise<boolean> {
  return page
    .getByRole("img", { name: lobby.pipa })
    .evaluateAll(
      (images, path) =>
        images.some(
          (image) =>
            image instanceof HTMLImageElement &&
            new URL(image.currentSrc || image.src).pathname === path &&
            image.complete &&
            image.naturalWidth > 0,
        ),
      artPath,
    );
}

test("SPEC-0044:E2E-01 o jogador logado abre o lobby e vê o banner da Pipa, a aba Crash e o cartão da Pipa com miniatura", async ({
  page,
}) => {
  const apogeuCalls: string[] = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname.startsWith("/api/v1/")) {
      apogeuCalls.push(request.url());
    }
  });
  const games = page.waitForResponse(isPath("/casino/games"));
  const banner = page.waitForResponse(isPath(pipaKit.banner));
  const thumbnail = page.waitForResponse(isPath(pipaKit.thumbnail));

  await enterCasino(page);

  // The catalog comes from the BFF of the casino.
  expect((await games).status()).toBe(200);

  // Banner: the featured game is the Pipa, with the banner art of its kit.
  const featured = page.getByRole("region", { name: lobby.pipa });
  await expect(featured).toBeVisible();
  await expect(
    featured.getByRole("heading", { name: lobby.pipa }),
  ).toBeVisible();
  expect((await banner).status()).toBe(200);

  // Category tabs: Todos, Crash and Novidades; Crash shows the Pipa.
  const categories = page.getByRole("tablist", { name: lobby.categories });
  await expect(categories.getByRole("tab", { name: lobby.all })).toBeVisible();
  await expect(categories.getByRole("tab", { name: lobby.news })).toBeVisible();
  const crash = categories.getByRole("tab", { name: lobby.crash });
  await expect(crash).toBeVisible();
  await crash.click();
  await expect(crash).toHaveAttribute("aria-selected", "true");

  // The Pipa card with the thumbnail of its kit, loaded.
  const panel = page.getByRole("tabpanel");
  await expect(panel.getByRole("heading", { name: lobby.pipa })).toBeVisible();
  expect((await thumbnail).status()).toBe(200);
  await expect
    .poll(() => pipaImageLoaded(page, pipaKit.thumbnail), {
      message: `the Pipa card shows ${pipaKit.thumbnail}`,
    })
    .toBe(true);

  await expectNoAxeViolations(page, "lobby");

  // §5: the browser never calls the Apogeu API.
  expect(apogeuCalls).toEqual([]);
});
