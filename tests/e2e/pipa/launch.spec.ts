import { expect, test } from "@playwright/test";

import { apiBaseUrl, openGame, roundTimeoutMs, text } from "./support/pipa";

// SPEC-0010 E2E-01: entrada válida pelo lançamento demo, em todos os projetos do Playwright (Chromium e WebKit mobile
// exigidos pela spec; Firefox também roda). Segurança (§5): o `lt` some da URL e nenhum token fica em armazenamento.

test("SPEC-0010:E2E-01 /dev/launch-demo abre o jogo com selo DEMO e histórico, sem lt na URL nem token guardado", async ({
  page,
}) => {
  test.setTimeout(roundTimeoutMs + 60_000);
  const api = apiBaseUrl();
  const sessionExchange = page.waitForResponse(
    (response) => response.request().method() === "POST" && response.url() === `${api}/api/v1/sessions`,
  );

  await openGame(page);

  const session = await sessionExchange;
  expect(session.status()).toBe(200);
  const { sessionToken } = (await session.json()) as { sessionToken: string };
  const launchToken = JSON.parse(session.request().postData() ?? "{}") as { launchToken?: string };

  await expect(page.getByText(text.demoBadge, { exact: true })).toBeVisible();
  const history = page.getByRole("list", { name: text.historyLabel });
  await expect(history).toBeVisible();
  await expect(history.getByRole("listitem").first()).toBeVisible({ timeout: roundTimeoutMs });

  // The launch token left the address bar (history.replaceState) and no token went to storage.
  expect(new URL(page.url()).searchParams.has("lt")).toBe(false);
  const stored = await page.evaluate(
    () => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }) + document.cookie,
  );
  expect(stored).not.toContain(sessionToken);
  expect(stored).not.toContain(launchToken.launchToken ?? "<no launch token sent>");
});
