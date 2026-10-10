// Placeholder of the SPEC-0018 scaffold: every entry point of the sound engine throws until it is implemented (Phase 2
// of the spec checklist). Keeps the arguments so the signatures stay checked by the compiler.

export function notImplemented(...args: readonly unknown[]): never {
  throw new Error("not implemented", { cause: args });
}
