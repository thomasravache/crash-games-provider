// Real environment of the integration and contract tests (SPEC-0010 §7.3, §7.4): the Host started by Aspire.
// `APOGEU_API_URL` is its base URL (for example http://localhost:5080), without a trailing slash.

interface ProcessLike {
  readonly env: Readonly<Record<string, string | undefined>>;
}

export function apiBaseUrl(): string {
  const env = (globalThis as { process?: ProcessLike }).process?.env ?? {};
  const value = env["APOGEU_API_URL"];
  if (value === undefined || value === "") {
    throw new Error(
      "APOGEU_API_URL is not set: the SPEC-0010 integration tests run against the Host started by Aspire " +
        "(dotnet run --project src/backend/AppHost), with APOGEU_API_URL=<base URL of the api resource>.",
    );
  }
  return value.replace(/\/+$/, "");
}

export interface LaunchDemo {
  readonly status: number;
  readonly location: string;
  readonly launchToken: string;
}

/** GET /dev/launch-demo (SPEC-0009@1, Development only) without following the 302: the demo launch URL. */
export async function launchDemo(api: string): Promise<LaunchDemo> {
  const response = await fetch(`${api}/dev/launch-demo`, { redirect: "manual" });
  const location = response.headers.get("location") ?? "";
  const launchToken = location === "" ? "" : (new URL(location, api).searchParams.get("lt") ?? "");
  return { status: response.status, location, launchToken };
}

/** Resolves with the first value `probe` returns that is not undefined, checked on every store change. */
export function waitUntil<T>(
  subscribe: (listener: () => void) => () => void,
  probe: () => T | undefined,
  timeoutMs: number,
  what: string,
): Promise<T> {
  return new Promise((resolve, reject) => {
    const first = probe();
    if (first !== undefined) {
      resolve(first);
      return;
    }
    const timer = setTimeout(() => {
      unsubscribe();
      reject(new Error(`Timed out after ${String(timeoutMs)} ms waiting for ${what}`));
    }, timeoutMs);
    const unsubscribe = subscribe(() => {
      const value = probe();
      if (value !== undefined) {
        clearTimeout(timer);
        unsubscribe();
        resolve(value);
      }
    });
  });
}
