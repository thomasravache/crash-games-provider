import type { ConfigEnv, UserConfig, UserConfigExport } from "vite";
import { describe, expect, test } from "vitest";

import gamePipaConfig from "../../../../src/frontend/apps/game-pipa/vite.config";

// SPEC-0050 UT-01: the dev server of the game accepts the hosts of the cloudflared quick tunnels (*.trycloudflare.com),
// so the game opens on a phone through the tunnel; Vite refuses unknown hosts otherwise.

const devServer: ConfigEnv = {
  command: "serve",
  mode: "development",
  isSsrBuild: false,
  isPreview: false,
};

async function resolveConfig(
  exported: UserConfigExport,
  env: ConfigEnv,
): Promise<UserConfig> {
  return typeof exported === "function" ? await exported(env) : await exported;
}

describe("SPEC-0050 acesso ao jogo local pelo túnel", () => {
  test("SPEC-0050:UT-01 o servidor de desenvolvimento do game-pipa aceita hosts .trycloudflare.com", async () => {
    const config = await resolveConfig(gamePipaConfig, devServer);

    // An explicit list: `true` would accept any host, which the spec does not ask for.
    const allowedHosts = config.server?.allowedHosts;
    expect(
      Array.isArray(allowedHosts),
      `server.allowedHosts is ${String(allowedHosts)}`,
    ).toBe(true);
    expect(allowedHosts).toContain(".trycloudflare.com");
  });
});
