// Generated from the backend OpenAPI document (ADR-0008, SPEC-0006). Never edit by hand.

import type { Multiplier } from "../scalars";

export interface paths {
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
}
export type webhooks = Record<string, never>;
export interface components {
  schemas: {
    MetaResponse: {
      apiVersion: string;
      /** Format: date-time */
      serverTime: string;
    };
    /** Format: multiplier */
    Multiplier: Multiplier;
    ProblemDetails: {
      detail?: null | string;
      instance?: null | string;
      /** Format: int32 */
      status?: null | number;
      title?: null | string;
      type?: null | string;
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
  };
  responses: never;
  parameters: never;
  requestBodies: never;
  headers: never;
  pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
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
}
