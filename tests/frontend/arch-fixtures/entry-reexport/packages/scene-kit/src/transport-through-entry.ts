// Arch fixture (SPEC-0003 IT-03): KNOWN VIOLATION. scene-kit gets the transport through the game-sdk package entry
// (src/index.ts, what "@apogeu/game-sdk" resolves to) instead of importing the transport path directly (ADR-0004).
import type { SocketClient } from "../../game-sdk/src/index";

export function connectFromScene(client: SocketClient): void {
  client.connect("wss://example.invalid/rounds");
}
