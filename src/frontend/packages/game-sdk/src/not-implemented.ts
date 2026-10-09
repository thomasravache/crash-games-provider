/** Scaffold of SPEC-0010: every public function throws this until the green phase implements it. */
export class NotImplementedError extends Error {
  /** Arguments the scaffold received, kept for debugging. */
  readonly inputs: readonly unknown[];

  constructor(what: string, ...inputs: readonly unknown[]) {
    super(`${what} is not implemented yet (SPEC-0010 scaffold).`);
    this.name = "NotImplementedError";
    this.inputs = inputs;
  }
}
