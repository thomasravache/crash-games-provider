// Shared steps of the demo casino E2E tests (SPEC-0043, SPEC-0035).
//
// Environment: the Aspire AppHost running (demo-site and DemoCasino among its resources), with
// APOGEU_DEMO_SITE_URL=<base URL of the demo-site resource>. The screens are found by roles and accessible names
// (ADR-0009), never by pixels or CSS classes.
import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";

/** Accessible names of the entry screen (pt-BR, Stitch "Cassino Demo — Entrada"). */
export const entry = {
  nickname: /apelido/iu,
  enter: /entrar/iu,
} as const;

/** Name of the casino session cookie (SPEC-0043 §6). */
export const sessionCookie = "casino_session";

export function demoSiteUrl(): string {
  const env =
    (
      globalThis as {
        process?: { env: Readonly<Record<string, string | undefined>> };
      }
    ).process?.env ?? {};
  const value = env["APOGEU_DEMO_SITE_URL"];
  if (value === undefined || value === "") {
    throw new Error(
      "APOGEU_DEMO_SITE_URL is not set: the SPEC-0043 E2E tests run against the Aspire environment " +
        "(dotnet run --project src/backend/AppHost), with APOGEU_DEMO_SITE_URL=<base URL of the demo-site resource>.",
    );
  }
  return value.replace(/\/+$/u, "");
}

/** A unique valid nickname: letters, digits and _, 3–20 characters. */
export function newNickname(): string {
  return `e2e_${Math.random().toString(36).slice(2, 10)}`;
}

/** WCAG 2.2 AA (§5): no axe violation on the current screen. */
export async function expectNoAxeViolations(
  page: Page,
  screen: string,
): Promise<void> {
  const accessibility = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    .analyze();
  expect(
    accessibility.violations,
    `axe violations on the ${screen} screen`,
  ).toEqual([]);
}
