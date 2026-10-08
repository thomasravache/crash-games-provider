// Arch fixture (SPEC-0003 IT-03): stands in for the network transport layer of @apogeu/game-sdk (ADR-0004).
export interface SocketClient {
  connect(url: string): void;
}
