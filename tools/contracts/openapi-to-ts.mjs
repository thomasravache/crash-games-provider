// REST contract generator (ADR-0008, SPEC-0006): OpenAPI document → TypeScript types with openapi-typescript.
// Money/Multiplier (`format: money|multiplier`) become the branded types; nullable schemas become `T | null`.
//
//   node tools/contracts/openapi-to-ts.mjs --input <openapi.json> --output <file.ts> [--scalars-from <module>]
//
// Without --scalars-from the branded types are declared in the output; with it they are imported from that module.
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";
import { formatTypeScript, importFromFrontend } from "./frontend-modules.mjs";
import { brandedFormats, scalarDeclarations } from "./scalars.mjs";

const header = "// Generated from the backend OpenAPI document (ADR-0008, SPEC-0006). Never edit by hand.\n";

/** TypeScript source of the REST contracts described by an OpenAPI document. */
export async function openApiToTypeScript(document, { scalarsFrom } = {}) {
  const openapiTypescript = await importFromFrontend("openapi-typescript");
  const { default: ts } = await importFromFrontend("typescript");
  const openapiTS = openapiTypescript.default.default ?? openapiTypescript.default;

  const usedBrands = new Set();
  const ast = await openapiTS(document, {
    alphabetize: true,
    transform(schema) {
      const brand = brandedFormats.get(schema.format);
      if (brand === undefined) {
        return undefined;
      }

      usedBrands.add(brand);
      const type = ts.factory.createTypeReferenceNode(brand);
      const types = Array.isArray(schema.type) ? schema.type : [schema.type];
      const nullable = schema.nullable === true || types.includes("null");
      return nullable
        ? ts.factory.createUnionTypeNode([type, ts.factory.createLiteralTypeNode(ts.factory.createNull())])
        : type;
    },
  });

  const types = openapiTypescript.astToString(ast);
  const scalars =
    scalarsFrom === undefined
      ? `${scalarDeclarations}\n\n`
      : usedBrands.size === 0
        ? ""
        : `import type { ${[...usedBrands].sort().join(", ")} } from "${scalarsFrom}";\n\n`;
  return formatTypeScript(`${header}\n${scalars}${types}`);
}

async function main() {
  const { values } = parseArgs({
    options: {
      input: { type: "string" },
      output: { type: "string" },
      "scalars-from": { type: "string" },
    },
  });
  if (values.input === undefined || values.output === undefined) {
    throw new Error("Usage: node tools/contracts/openapi-to-ts.mjs --input <openapi.json> --output <file.ts>");
  }

  const document = JSON.parse(await readFile(resolve(values.input), "utf8"));
  const source = await openApiToTypeScript(document, { scalarsFrom: values["scalars-from"] });
  const output = resolve(values.output);
  await mkdir(dirname(output), { recursive: true });
  await writeFile(output, source);
}

if (process.argv[1] !== undefined && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await main();
}
