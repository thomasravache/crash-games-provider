import { expect, test } from "@playwright/test";

import {
  demoSiteUrl,
  entry,
  expectNoAxeViolations,
  newNickname,
  sessionCookie,
} from "./support/demo-site";

// SPEC-0043 E2E-01: the player opens the demo casino, types a nickname and enters; they reach the lobby with the
// nickname at the top. Both screens pass axe (WCAG 2.2 AA). Security (§5): the session cookie is HttpOnly, so the page
// script never sees it.

test("SPEC-0043:E2E-01 o jogador entra no cassino demo com um apelido e chega ao lobby com o apelido no topo", async ({
  page,
  context,
}) => {
  const site = demoSiteUrl();
  const nickname = newNickname();

  await page.goto(site);
  const nicknameField = page.getByRole("textbox", { name: entry.nickname });
  await expect(nicknameField).toBeVisible();
  await expectNoAxeViolations(page, "entry");

  const sessionCreated = page.waitForResponse(
    (response) =>
      response.request().method() === "POST" &&
      new URL(response.url()).pathname === "/casino/session",
  );
  await nicknameField.fill(nickname);
  await page.getByRole("button", { name: entry.enter }).click();

  const session = await sessionCreated;
  expect(session.status()).toBe(200);

  // Lobby: the nickname at the top of the page (the banner landmark).
  await expect(page.getByRole("banner")).toContainText(nickname);
  await expect(nicknameField).toBeHidden();
  await expectNoAxeViolations(page, "lobby");

  const cookie = (await context.cookies(site)).find(
    (candidate) => candidate.name === sessionCookie,
  );
  expect(
    cookie,
    `${sessionCookie} must be set for the demo-site origin`,
  ).toBeDefined();
  expect(cookie?.httpOnly).toBe(true);
  const visibleToScript = await page.evaluate(() => document.cookie);
  expect(visibleToScript).not.toContain(sessionCookie);
});
