// Generated from the backend OpenAPI document (ADR-0008, SPEC-0006). Never edit by hand.

import type { Money, Multiplier } from "../scalars";

export interface paths {
  "/api/v1/backoffice/me": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    /** The authenticated backoffice user: id, Apogeu roles and operator. */
    get: operations["GetBackofficeMe"];
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  "/api/v1/games": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    /** The games enabled for the calling operator, with their currencies. */
    get: operations["ListGames"];
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  "/api/v1/me/bets": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    /** The bets of the session player, most recent first, paged by an opaque cursor. */
    get: operations["GetMyBets"];
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  "/api/v1/meta": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: operations["GetMeta"];
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  "/api/v1/operator/launch": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    /** Issues a single-use launch URL for a player (demo mode only). */
    post: operations["LaunchGame"];
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  "/api/v1/rounds/{roundId}/verification": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: operations["GetRoundVerification"];
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  "/api/v1/salas/{salaId}/rounds": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: operations["GetSalaRounds"];
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  "/api/v1/sessions": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    /** Exchanges a launch token for a player session. */
    post: operations["CreateSession"];
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  "/api/v1/wallet/balance": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    /** The balance of the session player in the session currency. */
    get: operations["GetWalletBalance"];
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  "/api/v1/wallet/demo/recharge": {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    /** Recharges the demo balance of the session player back to the opening amount when it is below the minimum bet. */
    post: operations["RechargeDemoWallet"];
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
}
export type webhooks = Record<string, never>;
export interface components {
  schemas: {
    BalanceResponse: {
      balance: components["schemas"]["Money"];
      currency: string;
      mode: string;
    };
    CreateSessionRequest: {
      launchToken: null | string;
    };
    ErrorResponse: {
      code: string;
      errors?: null | {
        [key: string]: string[];
      };
      /** Format: int32 */
      status: number;
      title: string;
    };
    GameSummaryResponse: {
      code: string;
      currencies: string[];
      name: string;
    };
    LaunchRequest: {
      currency: null | string;
      gameCode: null | string;
      language: null | string;
      mode: null | string;
      nickname: null | string;
      playerId: null | string;
    };
    LaunchResponse: {
      /** Format: date-time */
      expiresAt: string;
      /** Format: uri */
      launchUrl: string;
    };
    MeResponse: {
      operatorId: null | string;
      roles: string[];
      userId: string;
    };
    MetaResponse: {
      apiVersion: string;
      /** Format: date-time */
      serverTime: string;
    };
    /** Format: money */
    Money: Money;
    /** Format: multiplier */
    Multiplier: Multiplier;
    MyBetItem: {
      amount: components["schemas"]["Money"];
      autoCashOut: null | components["schemas"]["Multiplier"];
      /** Format: uuid */
      betId: string;
      currency: string;
      multiplier: null | components["schemas"]["Multiplier"];
      /** Format: int32 */
      panel: number;
      payout: null | components["schemas"]["Money"];
      /** Format: date-time */
      placedAt: string;
      /** Format: uuid */
      roundId: null | string;
      /** Format: int64 */
      roundNumber: null | number;
      status: components["schemas"]["MyBetStatus"];
    };
    MyBetsPage: {
      items: components["schemas"]["MyBetItem"][];
      nextCursor: null | string;
    };
    /** @enum {unknown} */
    MyBetStatus: "placed" | "queued" | "active" | "cashed_out" | "lost" | "cancelled" | "refunded";
    ProblemDetails: {
      detail?: null | string;
      instance?: null | string;
      /** Format: int32 */
      status?: null | number;
      title?: null | string;
      type?: null | string;
    };
    RechargeResponse: {
      balance: components["schemas"]["Money"];
      currency: string;
    };
    RoundHistoryItem: {
      /** Format: date-time */
      crashedAt: string;
      crashPoint: components["schemas"]["Multiplier"];
      /** Format: uuid */
      roundId: string;
      /** Format: int64 */
      roundNumber: number;
    };
    RoundVerificationResponse: {
      cap: components["schemas"]["Multiplier"];
      crashPoint: components["schemas"]["Multiplier"];
      previousSeed: string;
      rtp: string;
      salt: string;
      seed: string;
      terminalHash: string;
    };
    SessionPlayer: {
      nickname: null | string;
    };
    SessionResponse: {
      currency: string;
      /** Format: date-time */
      expiresAt: string;
      mode: string;
      player: components["schemas"]["SessionPlayer"];
      salaId: string;
      sessionToken: string;
    };
  };
  responses: never;
  parameters: never;
  requestBodies: never;
  headers: never;
  pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
  GetBackofficeMe: {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    requestBody?: never;
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["MeResponse"];
        };
      };
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown;
        };
        content?: never;
      };
      /** @description Forbidden */
      403: {
        headers: {
          [name: string]: unknown;
        };
        content?: never;
      };
    };
  };
  ListGames: {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    requestBody?: never;
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["GameSummaryResponse"][];
        };
      };
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
  GetMyBets: {
    parameters: {
      query?: {
        before?: string;
        limit?: number;
      };
      header?: never;
      path?: never;
      cookie?: never;
    };
    requestBody?: never;
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["MyBetsPage"];
        };
      };
      /** @description Bad Request */
      400: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
  GetMeta: {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    requestBody?: never;
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["MetaResponse"];
        };
      };
    };
  };
  LaunchGame: {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    requestBody: {
      content: {
        "application/json": components["schemas"]["LaunchRequest"];
      };
    };
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["LaunchResponse"];
        };
      };
      /** @description Bad Request */
      400: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
      /** @description Forbidden */
      403: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
      /** @description Conflict */
      409: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
      /** @description Too Many Requests */
      429: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
  GetRoundVerification: {
    parameters: {
      query?: never;
      header?: never;
      path: {
        roundId: string;
      };
      cookie?: never;
    };
    requestBody?: never;
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["RoundVerificationResponse"];
        };
      };
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ProblemDetails"];
        };
      };
      /** @description Conflict */
      409: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ProblemDetails"];
        };
      };
    };
  };
  GetSalaRounds: {
    parameters: {
      query?: {
        limit?: number;
      };
      header?: never;
      path: {
        salaId: string;
      };
      cookie?: never;
    };
    requestBody?: never;
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["RoundHistoryItem"][];
        };
      };
      /** @description Bad Request */
      400: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ProblemDetails"];
        };
      };
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ProblemDetails"];
        };
      };
    };
  };
  CreateSession: {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    requestBody: {
      content: {
        "application/json": components["schemas"]["CreateSessionRequest"];
      };
    };
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["SessionResponse"];
        };
      };
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
      /** @description Too Many Requests */
      429: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
  GetWalletBalance: {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    requestBody?: never;
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["BalanceResponse"];
        };
      };
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
  RechargeDemoWallet: {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    requestBody?: never;
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/json": components["schemas"]["RechargeResponse"];
        };
      };
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
      /** @description Forbidden */
      403: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
      /** @description Conflict */
      409: {
        headers: {
          [name: string]: unknown;
        };
        content: {
          "application/problem+json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
}
