// Placeholder of the SPEC-0026 scaffold: the bets SDK throws until it is implemented. Keeps the arguments so the
// signatures stay checked by the compiler.

export function notImplemented(...args: readonly unknown[]): never {
  throw new Error("not implemented", { cause: args });
}
