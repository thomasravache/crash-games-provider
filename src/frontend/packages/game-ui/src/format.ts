// pt-BR formatting of what the server sent (ADR-0008: decimals arrive as strings and are only formatted).

const twoPlaces = new Intl.NumberFormat("pt-BR", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** "2.47" or 2.47 → "2,47x". */
export function formatMultiplier(value: number | string): string {
  const number = typeof value === "number" ? value : Number(value);
  return `${twoPlaces.format(Number.isFinite(number) ? number : 1)}x`;
}

/** Whole seconds left until `endsAt` (ISO, server clock), with the server offset applied to the local clock. */
export function secondsLeft(
  endsAt: string | null,
  serverOffsetMs: number,
  localNowMs: number,
): number {
  if (endsAt === null) return 0;
  const left = Date.parse(endsAt) - (localNowMs + serverOffsetMs);
  return Number.isFinite(left) ? Math.max(0, Math.ceil(left / 1000)) : 0;
}
