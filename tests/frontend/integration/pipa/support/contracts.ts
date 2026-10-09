// Published contracts the CTs validate against (ADR-0008): the hub manifest exported from the strongly typed
// RoundsHub (SPEC-0008@2) and the versioned OpenAPI document (SPEC-0009@1 sessions, SPEC-0008@2 rounds REST).
import { execFile } from "node:child_process";
import { existsSync, statSync } from "node:fs";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

import openApiDocument from "../../../../../artifacts/openapi/v1.json";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../../../..");

export const openApi: unknown = openApiDocument;

export interface HubMethod {
  readonly name: string;
  readonly parameters: readonly { readonly name: string; readonly schema: unknown }[];
  readonly returns: unknown;
}

export interface HubManifest {
  readonly hubs: readonly {
    readonly name: string;
    readonly server: readonly HubMethod[];
    readonly client: readonly HubMethod[];
  }[];
}

/** Runs the hub exporter built by `dotnet build Apogeu.slnx` (tools/contracts/HubContracts), as contracts:generate does. */
export async function exportHubManifest(): Promise<HubManifest> {
  const binaries = path.join(repositoryRoot, "tools", "contracts", "HubContracts", "bin");
  const candidates = ["Debug", "Release"]
    .map((configuration) => path.join(binaries, configuration, "net10.0", "Apogeu.HubContracts.dll"))
    .filter((candidate) => existsSync(candidate))
    .sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs);
  const exporter = candidates[0];
  if (exporter === undefined) {
    throw new Error("Apogeu.HubContracts.dll not found: run `dotnet build Apogeu.slnx` first.");
  }
  const scratch = await mkdtemp(path.join(tmpdir(), "apogeu-spec-0010-hubs-"));
  try {
    const output = path.join(scratch, "hubs.json");
    await new Promise<void>((resolve, reject) => {
      execFile("dotnet", [exporter, "--output", output], (error, _stdout, stderr) => {
        if (error === null) resolve();
        else reject(new Error(`Hub exporter failed: ${stderr}`));
      });
    });
    return JSON.parse(await readFile(output, "utf8")) as HubManifest;
  } finally {
    await rm(scratch, { recursive: true, force: true });
  }
}

/** JSON Schema of a RoundsHub client message (its only parameter) or of a server method result. */
export function hubSchema(manifest: HubManifest, name: string): unknown {
  const hub = manifest.hubs.find((candidate) => candidate.name === "RoundsHub");
  if (hub === undefined) throw new Error("RoundsHub is not in the hub manifest");
  const client = hub.client.find((method) => method.name === name);
  if (client !== undefined) return client.parameters[0]?.schema;
  const server = hub.server.find((method) => method.name === name);
  if (server !== undefined) return server.returns;
  throw new Error(`RoundsHub has no method ${name}`);
}

/** Schema of an OpenAPI operation part: the request body or the response with `status`. */
export function openApiSchema(
  pathName: string,
  method: "get" | "post",
  part: "request" | number,
  contentType: string,
): unknown {
  const document = openApi as {
    paths?: Record<string, Record<string, {
      requestBody?: { content?: Record<string, { schema?: unknown }> };
      responses?: Record<string, { content?: Record<string, { schema?: unknown }> }>;
    }>>;
  };
  const operation = document.paths?.[pathName]?.[method];
  if (operation === undefined) {
    throw new Error(`${method.toUpperCase()} ${pathName} is not in artifacts/openapi/v1.json`);
  }
  const content =
    part === "request"
      ? operation.requestBody?.content
      : operation.responses?.[String(part)]?.content;
  const schema = content?.[contentType]?.schema;
  if (schema === undefined) {
    throw new Error(`${method.toUpperCase()} ${pathName} documents no ${contentType} schema for ${String(part)}`);
  }
  return schema;
}
