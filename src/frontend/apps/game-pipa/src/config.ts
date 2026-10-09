// Runtime configuration of the game (SPEC-0010): the API base URL comes from VITE_APOGEU_API_URL (injected by the
// AppHost); without it the game talks to its own origin (a reverse proxy in front of both).

export const demoBalance = "R$ 1.000,00";

export function apiBaseUrl(): string {
  const configured = import.meta.env.VITE_APOGEU_API_URL;
  const base = configured === undefined || configured === "" ? window.location.origin : configured;
  return base.replace(/\/+$/u, "");
}
