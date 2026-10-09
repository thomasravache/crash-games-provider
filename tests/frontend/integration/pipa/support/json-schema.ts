// Minimal JSON Schema checker for the contract tests (SPEC-0010 CT-02, CT-03): the subset the backend generators
// emit (System.Text.Json schema exporter for the hubs, ASP.NET Core OpenAPI 3.1 for REST). Strict on objects: a
// property the schema does not declare is a contract break (for example a crash point leaking before the crash).

type Schema = boolean | { readonly [keyword: string]: unknown };

export function schemaErrors(schema: unknown, value: unknown, root: unknown = schema, at = "$"): string[] {
  if (schema === true || schema === undefined) return [];
  if (schema === false) return [`${at}: no value is allowed`];
  const s = schema as Exclude<Schema, boolean>;

  if (typeof s["$ref"] === "string") {
    return schemaErrors(resolveRef(root, s["$ref"]), value, root, at);
  }
  for (const keyword of ["anyOf", "oneOf"] as const) {
    const branches = s[keyword];
    if (Array.isArray(branches)) {
      const results = branches.map((branch) => schemaErrors(branch, value, root, at));
      if (!results.some((errors) => errors.length === 0)) {
        return [`${at}: matches no branch of ${keyword} (${results.map((e) => e.join("; ")).join(" | ")})`];
      }
    }
  }
  if (Array.isArray(s["allOf"])) {
    const all = (s["allOf"] as unknown[]).flatMap((branch) => schemaErrors(branch, value, root, at));
    if (all.length > 0) return all;
  }
  if (Array.isArray(s["enum"]) && !(s["enum"] as unknown[]).some((option) => option === value)) {
    return [`${at}: ${JSON.stringify(value)} is not one of ${JSON.stringify(s["enum"])}`];
  }
  if ("const" in s && s["const"] !== value) {
    return [`${at}: ${JSON.stringify(value)} is not ${JSON.stringify(s["const"])}`];
  }

  const types = typeList(s["type"]);
  if (types.length > 0 && !types.some((type) => hasType(value, type))) {
    return [`${at}: ${JSON.stringify(value)} is not of type ${types.join("|")}`];
  }

  const errors: string[] = [];
  if (typeof value === "string") {
    if (typeof s["pattern"] === "string" && !new RegExp(s["pattern"], "u").test(value)) {
      errors.push(`${at}: "${value}" does not match ${s["pattern"]}`);
    }
    if (s["format"] === "date-time" && Number.isNaN(Date.parse(value))) {
      errors.push(`${at}: "${value}" is not an ISO-8601 date-time`);
    }
  }
  if (Array.isArray(value) && s["items"] !== undefined) {
    value.forEach((item, index) => {
      errors.push(...schemaErrors(s["items"], item, root, `${at}[${String(index)}]`));
    });
  }
  if (isObject(value)) {
    const properties = (s["properties"] ?? {}) as Record<string, unknown>;
    for (const name of (s["required"] ?? []) as string[]) {
      if (!(name in value)) errors.push(`${at}: missing required property "${name}"`);
    }
    for (const [name, item] of Object.entries(value)) {
      if (name in properties) {
        errors.push(...schemaErrors(properties[name], item, root, `${at}.${name}`));
      } else if (s["additionalProperties"] !== undefined && s["additionalProperties"] !== false) {
        errors.push(...schemaErrors(s["additionalProperties"], item, root, `${at}.${name}`));
      } else if (s["properties"] !== undefined) {
        errors.push(`${at}: property "${name}" is not in the contract`);
      }
    }
  }
  return errors;
}

function typeList(type: unknown): string[] {
  if (typeof type === "string") return [type];
  return Array.isArray(type) ? (type as string[]) : [];
}

function hasType(value: unknown, type: string): boolean {
  switch (type) {
    case "null":
      return value === null;
    case "string":
      return typeof value === "string";
    case "boolean":
      return typeof value === "boolean";
    case "integer":
      return Number.isInteger(value);
    case "number":
      return typeof value === "number";
    case "array":
      return Array.isArray(value);
    case "object":
      return isObject(value);
    default:
      return false;
  }
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function resolveRef(root: unknown, ref: string): unknown {
  if (!ref.startsWith("#")) throw new Error(`Only local $ref is supported: ${ref}`);
  return ref
    .slice(1)
    .split("/")
    .filter((part) => part !== "")
    .map((part) => part.replaceAll("~1", "/").replaceAll("~0", "~"))
    .reduce<unknown>((node, part) => (isObject(node) ? node[part] : undefined), root);
}
