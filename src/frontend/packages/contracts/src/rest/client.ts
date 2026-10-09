// Generated from the backend (ADR-0008, SPEC-0006). Never edit by hand.

import createClient, { type Client, type ClientOptions } from "openapi-fetch";
import type { paths } from "./schema";

/** Typed client of the REST API (openapi-fetch): paths, parameters and bodies come from the OpenAPI document. */
export type RestClient = Client<paths>;

export function createRestClient(options?: ClientOptions): RestClient {
  return createClient<paths>(options);
}
// ci-live probe CT-01: hand edit, out of sync with contracts:generate
