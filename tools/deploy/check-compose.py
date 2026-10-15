#!/usr/bin/env python3
"""Contract SPEC-0065@3 of the published compose, as tools/deploy/vps-deploy reads it (SPEC-0066 CT-01).

Reads `docker compose config --format json --no-interpolate` on stdin (references like ${VAR} kept, so no value of the
.env is ever in memory here) and checks: every service and volume of the contract exists; the long-running services
(--networked) share one single network, the one the proxy joins (infra/caddy/compose.proxy.yaml); no service of the
compose publishes ports on the host (only the proxy does, and it is not in this file — SPEC-0066 Emenda v4); every
variable the compose references without a default is defined in one of the --env-file files (only the
names of those files are read, never their values).

Exit 0 when the compose honours the contract; otherwise exit 1 and one line on stdout naming what is missing.
"""

from __future__ import annotations

import argparse
import json
import re
import sys

PROXY_NETWORK = "aspire"
REFERENCE = re.compile(r"\$\{([A-Za-z_][A-Za-z0-9_]*)(:?[-?+=][^}]*)?\}")


def strings(node: object):
    if isinstance(node, str):
        yield node
    elif isinstance(node, dict):
        for key, value in node.items():
            yield key
            yield from strings(value)
    elif isinstance(node, list):
        for item in node:
            yield from strings(item)


def defined_names(paths: list[str]) -> set[str]:
    names: set[str] = set()
    for path in paths:
        with open(path, encoding="utf-8") as env:
            for line in env:
                line = line.strip()
                if line.startswith("export "):
                    line = line[len("export "):].lstrip()
                if not line or line.startswith("#") or "=" not in line:
                    continue
                names.add(line.split("=", 1)[0].strip())
    return names


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--services", required=True, help="space-separated services of the contract")
    parser.add_argument("--volumes", required=True, help="space-separated volumes of the contract")
    parser.add_argument("--networked", required=True, help="space-separated services that must be on the proxy network")
    parser.add_argument("--env-file", action="append", default=[], help="files defining the variables")
    arguments = parser.parse_args()

    try:
        compose = json.load(sys.stdin)
    except json.JSONDecodeError:
        print("docker-compose.yaml is not a valid compose file")
        return 1

    problems: list[str] = []
    services = compose.get("services") or {}
    missing_services = [name for name in arguments.services.split() if name not in services]
    if missing_services:
        problems.append("missing service(s) " + ", ".join(missing_services))

    volumes = compose.get("volumes") or {}
    missing_volumes = [name for name in arguments.volumes.split() if name not in volumes]
    if missing_volumes:
        problems.append("missing volume(s) " + ", ".join(missing_volumes))

    declared = set((compose.get("networks") or {}).keys()) - {"default"}
    if declared != {PROXY_NETWORK}:
        problems.append(f"expected the single network '{PROXY_NETWORK}', found: {', '.join(sorted(declared)) or 'none'}")
    for name in arguments.networked.split():
        service = services.get(name)
        if service is not None and PROXY_NETWORK not in (service.get("networks") or {}):
            problems.append(f"service {name} is not on the network '{PROXY_NETWORK}'")
    publishing = sorted(name for name, service in services.items() if (service or {}).get("ports"))
    if publishing:
        problems.append("only the proxy may publish ports on the host; publishing: " + ", ".join(publishing))

    defined = defined_names(arguments.env_file)
    undefined = sorted(
        {
            match.group(1)
            for text in strings(compose)
            for match in REFERENCE.finditer(text)
            if not (match.group(2) or "").lstrip(":").startswith(("-", "+"))
        }
        - defined
    )
    if undefined:
        problems.append("variable(s) without a value in the .env: " + ", ".join(undefined))

    if problems:
        print("; ".join(problems))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
