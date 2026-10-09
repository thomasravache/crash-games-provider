// Contract scalars (ADR-0008): values that travel as decimal strings and are branded in TypeScript so the client
// formats them but never does arithmetic on them. The OpenAPI document marks them with `format` (SPEC-0006).

/** OpenAPI `format` → branded TypeScript type. */
export const brandedFormats = new Map([
  ["money", "Money"],
  ["multiplier", "Multiplier"],
]);

/** Declarations of the branded types, shared by the REST and hub contracts. */
export const scalarDeclarations = [...brandedFormats.values()]
  .map(
    (name) =>
      `/** Decimal string (for example "${name === "Money" ? "10.50" : "2.47"}"); format it, never compute with it (ADR-0008). */\n` +
      `export type ${name} = string & { readonly __brand: "${name}" };`,
  )
  .join("\n\n");
