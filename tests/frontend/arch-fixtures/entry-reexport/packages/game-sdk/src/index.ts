// Arch fixture (SPEC-0003 IT-03): the package entry of @apogeu/game-sdk re-exporting the transport. A scene that
// imports this entry reaches the network layer even though it never names the transport path (ADR-0004).
export type { SocketClient } from "./transport/socket-client";

export interface RoundState {
  readonly multiplier: number;
}
