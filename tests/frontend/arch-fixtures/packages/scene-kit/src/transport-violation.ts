// Arch fixture (SPEC-0003 IT-03): KNOWN VIOLATION. scene-kit must not import the network layer or transport types of
// game-sdk; it only reads the state game-sdk exposes (ADR-0004). dependency-cruiser must reject this import.
import type { SocketClient } from "../../game-sdk/src/transport/socket-client";

export function connectFromScene(client: SocketClient): void {
  client.connect("wss://example.invalid/rounds");
}
