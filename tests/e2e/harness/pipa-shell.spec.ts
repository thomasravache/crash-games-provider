import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

// SPEC-0003 E2E-01: proves the E2E harness (Playwright + axe) against the minimal game-pipa app served by Vite.
// Runs in every browser project of src/frontend/playwright.config.ts (Chromium and WebKit mobile at least); the
// config's webServer starts game-pipa and sets baseURL.

test("SPEC-0003:E2E-01 o app game-pipa abre com o título Pipa visível e sem violações axe", async ({
  page,
}) => {
  await page.goto("/");

  await expect(page).toHaveTitle("Pipa");
  await expect(page.getByRole("heading", { level: 1, name: "Pipa" })).toBeVisible();

  const accessibility = await new AxeBuilder({ page }).analyze();
  expect(accessibility.violations).toEqual([]);
});
