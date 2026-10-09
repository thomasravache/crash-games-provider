// Generated from the backend (ADR-0008, SPEC-0006). Never edit by hand.

/** Decimal string (for example "10.50"); format it, never compute with it (ADR-0008). */
export type Money = string & { readonly __brand: "Money" };

/** Decimal string (for example "2.47"); format it, never compute with it (ADR-0008). */
export type Multiplier = string & { readonly __brand: "Multiplier" };
