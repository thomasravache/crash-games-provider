// Hub contract generator (ADR-0008, SPEC-0006): hub manifest (exported by HubContracts from the strongly typed SignalR
// hubs) → TypeScript interfaces. Server methods become `Name(args): Promise<Result>` (streams: `AsyncIterable<Item>`);
// client methods (TClient) become `Name(args): void`. Method names are the wire names the hub uses.
//
//   node tools/contracts/hubs-to-ts.mjs --input <manifest.json> --output <file.ts> --scalars-from <module>
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";
import { formatTypeScript } from "./frontend-modules.mjs";
import { brandedFormats } from "./scalars.mjs";

const header = "// Generated from the backend SignalR hubs (ADR-0008, SPEC-0006). Never edit by hand.\n";

/** TypeScript source of the hub contracts described by a hub manifest (`{ hubs: [...] }`). */
export async function hubsToTypeScript(manifest, { scalarsFrom }) {
  const usedBrands = new Set();
  const toType = (schema) => schemaToType(schema, usedBrands);

  const declarations = manifest.hubs.map((hub) => {
    const signature = (method) =>
      method.parameters.map((parameter) => `${parameter.name}: ${toType(parameter.schema)}`).join(", ");
    const server = hub.server.map((method) => {
      const result = method.returns === null ? "void" : toType(method.returns);
      const returns = method.stream ? `AsyncIterable<${result}>` : `Promise<${result}>`;
      return `  ${method.name}(${signature(method)}): ${returns};`;
    });
    const client = hub.client.map((method) => `  ${method.name}(${signature(method)}): void;`);

    return [
      `/** Methods the client invokes on ${hub.name}. */`,
      `export interface ${hub.name}Server {`,
      ...server,
      "}",
      "",
      `/** Methods ${hub.name} calls on the client. */`,
      `export interface ${hub.name}Client {`,
      ...client,
      "}",
    ].join("\n");
  });

  const names = manifest.hubs.map((hub) => JSON.stringify(hub.name)).join(", ");
  const imports =
    usedBrands.size === 0
      ? ""
      : `import type { ${[...usedBrands].sort().join(", ")} } from "${scalarsFrom}";\n\n`;
  const source = [
    header,
    imports,
    "/** Names of the SignalR hubs the backend serves. */",
    `export const hubNames = [${names}] as const;`,
    "",
    "export type HubName = (typeof hubNames)[number];",
    "",
    ...declarations.map((declaration) => `${declaration}\n`),
  ].join("\n");
  return formatTypeScript(source);
}

/** TypeScript type of a JSON Schema (System.Text.Json schema exporter output, contract conventions). */
function schemaToType(schema, usedBrands) {
  if (schema === true || schema === undefined || schema === null) {
    return "unknown";
  }
  if (schema === false) {
    return "never";
  }

  const union = (types) => [...new Set(types)].join(" | ");
  for (const keyword of ["anyOf", "oneOf"]) {
    if (Array.isArray(schema[keyword])) {
      return union(schema[keyword].map((branch) => schemaToType(branch, usedBrands)));
    }
  }

  const types = Array.isArray(schema.type) ? schema.type : schema.type === undefined ? [] : [schema.type];
  const nullable = types.includes("null") ? ["null"] : [];
  const brand = brandedFormats.get(schema.format);
  if (brand !== undefined) {
    usedBrands.add(brand);
    return union([brand, ...nullable]);
  }
  if (Array.isArray(schema.enum)) {
    return union(schema.enum.map((value) => JSON.stringify(value)));
  }
  if (types.length === 0) {
    return "unknown"; // $ref (recursive types) or an unconstrained schema
  }

  return union(
    types.map((type) => {
      switch (type) {
        case "string":
          return "string";
        case "integer":
        case "number":
          return "number";
        case "boolean":
          return "boolean";
        case "null":
          return "null";
        case "array":
          return `${wrap(schemaToType(schema.items, usedBrands))}[]`;
        case "object":
          return objectType(schema, usedBrands);
        default:
          return "unknown";
      }
    }),
  );
}

function objectType(schema, usedBrands) {
  const properties = Object.entries(schema.properties ?? {});
  if (properties.length === 0) {
    const values = schema.additionalProperties;
    return values === undefined || values === false
      ? "Record<string, never>"
      : `Record<string, ${schemaToType(values, usedBrands)}>`;
  }

  const required = new Set(schema.required ?? []);
  const members = properties.map(
    ([name, property]) =>
      `${JSON.stringify(name)}${required.has(name) ? "" : "?"}: ${schemaToType(property, usedBrands)};`,
  );
  return `{ ${members.join(" ")} }`;
}

function wrap(type) {
  return type.includes(" ") ? `(${type})` : type;
}

async function main() {
  const { values } = parseArgs({
    options: {
      input: { type: "string" },
      output: { type: "string" },
      "scalars-from": { type: "string" },
    },
  });
  if (values.input === undefined || values.output === undefined || values["scalars-from"] === undefined) {
    throw new Error(
      "Usage: node tools/contracts/hubs-to-ts.mjs --input <manifest.json> --output <file.ts> --scalars-from <module>",
    );
  }

  const manifest = JSON.parse(await readFile(resolve(values.input), "utf8"));
  const source = await hubsToTypeScript(manifest, { scalarsFrom: values["scalars-from"] });
  const output = resolve(values.output);
  await mkdir(dirname(output), { recursive: true });
  await writeFile(output, source);
}

if (process.argv[1] !== undefined && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await main();
}
