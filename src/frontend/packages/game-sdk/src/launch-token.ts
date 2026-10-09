// Launch token in the URL (SPEC-0010 §5): read `lt` from `/?lt=…` and remove it with history.replaceState, so the
// single-use token never stays in the address bar, the history or a shared link.

export interface LaunchLocation {
  readonly href: string;
}

export interface LaunchHistory {
  replaceState(data: unknown, unused: string, url?: string | URL | null): void;
}

/** Returns the launch token (or null) and removes `lt` from the address bar. */
export function takeLaunchToken(location: LaunchLocation, history: LaunchHistory): string | null {
  const url = new URL(location.href);
  if (!url.searchParams.has("lt")) return null;
  const token = url.searchParams.get("lt") ?? "";
  url.searchParams.delete("lt");
  history.replaceState(null, "", `${url.pathname}${url.search}${url.hash}`);
  return token === "" ? null : token;
}
