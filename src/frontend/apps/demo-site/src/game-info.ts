// Fixed data of the games of the demo casino (SPEC-0045 §4, Emenda v1): the "Sobre o jogo" column. The public catalog of
// the Apogeu does not expose RTP nor the maximum win, so the demo-site keeps them per game; a game without fixed data
// shows only its provider.

/** The studio of every game of the catalog. */
export const provider = "Apogeu Games";

export interface GameAbout {
  readonly name: string;
  readonly rtp: string;
  readonly maxWin: string;
  readonly provablyFair: boolean;
}

const aboutByCode: Readonly<Record<string, GameAbout>> = {
  pipa: { name: "Pipa", rtp: "97%", maxWin: "10.000x", provablyFair: true },
};

/** The fixed data of a game, or undefined when the demo-site has none for it. */
export function aboutGame(code: string): GameAbout | undefined {
  return Object.hasOwn(aboutByCode, code) ? aboutByCode[code] : undefined;
}
