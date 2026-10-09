import type { JSX } from "react";

import { NotImplementedError } from "./not-implemented";

/** Error screen for an invalid, expired or used launch token: "Sessão expirada. Volte ao cassino para jogar." */
export function SessionExpiredScreen(): JSX.Element {
  throw new NotImplementedError("SessionExpiredScreen");
}
