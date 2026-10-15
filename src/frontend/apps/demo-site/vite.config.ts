import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

/**
 * SPEC-0043 (ADR-0027): the browser only talks to the casino BFF (DemoCasino) under /casino, on its own origin, so the
 * casino_session cookie belongs to the demo-site. In development the Vite server forwards /casino to the DemoCasino at
 * DEMO_CASINO_URL (injected by the Aspire AppHost). The variable has no VITE_ prefix: it never reaches the bundle, and
 * neither does any operator credential (the HMAC secret only exists in the DemoCasino).
 */
const casinoPath = "/casino";

export default defineConfig(({ mode }) => {
  const casinoUrl = loadEnv(mode, ".", "DEMO_CASINO_")["DEMO_CASINO_URL"];
  return {
    plugins: [react()],
    ...(casinoUrl === undefined || casinoUrl === ""
      ? {}
      : { server: { proxy: { [casinoPath]: { target: casinoUrl } } } }),
  };
});
