// Placeholder of the SPEC-0011 scaffold: every subsystem entry point throws until it is implemented (Phase 2 of the
// spec checklist). Keeps the arguments so the signatures stay checked by the compiler.

export function notImplemented(...args: readonly unknown[]): never {
  throw new Error("not implemented", { cause: args });
}
