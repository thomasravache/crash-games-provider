// Placeholder of the SPEC-0018 scaffold: the sound control throws until it is implemented. Keeps the arguments so the
// signatures stay checked by the compiler.

export function notImplemented(...args: readonly unknown[]): never {
  throw new Error("not implemented", { cause: args });
}
