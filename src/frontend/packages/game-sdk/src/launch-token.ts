// Launch token in the URL (SPEC-0010 §5): read `lt` from `/?lt=…` and remove it with history.replaceState.

import { NotImplementedError } from "./not-implemented";

export interface LaunchLocation {
  readonly href: string;
}

export interface LaunchHistory {
  replaceState(data: unknown, unused: string, url?: string | URL | null): void;
}

/** Returns the launch token (or null) and removes `lt` from the address bar. */
export function takeLaunchToken(location: LaunchLocation, history: LaunchHistory): string | null {
  throw new NotImplementedError("takeLaunchToken", history, location);
}
