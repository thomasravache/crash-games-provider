// The two pages of the demo casino (SPEC-0044, SPEC-0045): the lobby at "/" and the game page at "/jogo/{gameCode}".
// History API only, no router dependency.

export type Route = { readonly page: "lobby" } | { readonly page: "game"; readonly code: string };

const gamePrefix = "/jogo/";

export function gamePath(code: string): string {
  return gamePrefix + encodeURIComponent(code);
}

export function routeOf(pathname: string): Route {
  if (!pathname.startsWith(gamePrefix)) {
    return { page: "lobby" };
  }
  const segment = pathname.slice(gamePrefix.length).replace(/\/+$/u, "");
  if (segment === "" || segment.includes("/")) {
    return { page: "lobby" };
  }
  try {
    return { page: "game", code: decodeURIComponent(segment) };
  } catch {
    return { page: "lobby" };
  }
}

/** Moves to a path of the casino without reloading the page. */
export function navigate(path: string): void {
  if (window.location.pathname !== path) {
    window.history.pushState(null, "", path);
  }
}
