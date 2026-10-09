#!/usr/bin/env python3
"""Reference generator of the provably-fair vectors v1 (SPEC-0007 §4, ADR-0006).

This script is the independent oracle of tests/vectors/fairness-v1.json: it follows the algorithm v1 of SPEC-0007 §4
literally, with the Python standard library only, and shares no code with the C# Fairness module or the TypeScript
verifier. Re-running it must reproduce fairness-v1.json byte for byte:

    python3 tests/vectors/generate_fairness_v1.py            # rewrite fairness-v1.json
    python3 tests/vectors/generate_fairness_v1.py --check    # exit 1 if fairness-v1.json differs

Algorithm v1 (SPEC-0007 §4), with the encoding choices made explicit:
  1. Chain: h_0 = 32 bytes; h_{k+1} = SHA256(h_k) over the raw 32-byte digest (never over its hex text).
  2. Round r (1..N) uses s_r = h_{N-r}; chaining: SHA256(s_r) = s_{r-1}, with s_0 = h_N (the terminal hash).
  3. H = HMAC-SHA256(key = the 32 raw bytes of s_r, message = the raw bytes of the salt, i.e. the hex-decoded salt,
     never the UTF-8 text of its hex); X = the first 52 bits of H read big-endian as an unsigned integer (= the first
     13 hex digits of H); E = 2^52.
  4. crash = min(cap, max(1.00, floor(100 * RTP * E / (E - X)) / 100)), in exact integer arithmetic: RTP = p/q exactly
     (0.97 = 97/100), so floor(100 * p * E / (q * (E - X))) hundredths, then the max/min in hundredths.
  5. In JSON, seeds and salts are lowercase hex; rtp, cap and crashPoint are decimal strings (rtp with 2 decimals,
     cap and crashPoint with exactly 2 decimals).
"""

import hashlib
import hmac
import json
import sys
from decimal import Decimal
from fractions import Fraction
from pathlib import Path

E = 1 << 52
OUTPUT = Path(__file__).with_name("fairness-v1.json")

# Fixed, public inputs of the vectors: the origin and the salt are SHA-256 of fixed labels, so anyone can recompute them.
ORIGIN_LABEL = b"apogeu/fairness-v1/vectors/origin"
SALT_LABEL = b"apogeu/fairness-v1/vectors/salt"
CHAIN_LENGTH = 100_000
DEFAULT_CAP = "10000.00"


def sha256(data: bytes) -> bytes:
    return hashlib.sha256(data).digest()


def sample(seed: bytes, salt: bytes) -> int:
    """X: the first 52 bits of HMAC-SHA256(key = seed, message = salt), big-endian."""
    digest = hmac.new(seed, salt, hashlib.sha256).digest()
    return int.from_bytes(digest[:7], "big") >> 4


def raw_hundredths(x: int, rtp: str) -> int:
    """floor(100 * RTP * E / (E - X)), before the 1.00 floor and the cap, with RTP as an exact decimal."""
    rtp_fraction = Fraction(Decimal(rtp))
    return (100 * rtp_fraction.numerator * E) // (rtp_fraction.denominator * (E - x))


def crash_hundredths(x: int, rtp: str, cap: str) -> int:
    """The crash point in hundredths (an integer): min(cap, max(1.00, raw))."""
    cap_hundredths = Fraction(Decimal(cap)) * 100
    assert cap_hundredths.denominator == 1, "cap must have at most 2 decimals"
    return min(int(cap_hundredths), max(100, raw_hundredths(x, rtp)))


def format_hundredths(value: int) -> str:
    return f"{value // 100}.{value % 100:02d}"


def compute(seed: bytes, salt: bytes, rtp: str, cap: str) -> str:
    return format_hundredths(crash_hundredths(sample(seed, salt), rtp, cap))


def build_chain() -> list:
    chain = [sha256(ORIGIN_LABEL)]
    for _ in range(CHAIN_LENGTH):
        chain.append(sha256(chain[-1]))
    return chain


def vector(chain: list, salt: bytes, round_index: int, rtp: str, cap: str, description: str) -> dict:
    seed = chain[CHAIN_LENGTH - round_index]
    previous = chain[CHAIN_LENGTH - round_index + 1]
    assert sha256(seed) == previous
    return {
        "description": description,
        "seed": seed.hex(),
        "previousSeed": previous.hex(),
        "salt": salt.hex(),
        "rtp": rtp,
        "cap": cap,
        "crashPoint": compute(seed, salt, rtp, cap),
    }


def first_round(chain: list, salt: bytes, rtp: str, cap: str, predicate) -> int:
    for round_index in range(1, CHAIN_LENGTH + 1):
        x = sample(chain[CHAIN_LENGTH - round_index], salt)
        if predicate(x, crash_hundredths(x, rtp, cap)):
            return round_index
    raise RuntimeError("no round of the chain satisfies the predicate")


def build_document() -> dict:
    chain = build_chain()
    salt = sha256(SALT_LABEL)

    first_three = {sample(chain[CHAIN_LENGTH - r], salt) for r in (1, 2, 3)}
    instant = first_round(chain, salt, "0.97", DEFAULT_CAP, lambda x, c: raw_hundredths(x, "0.97") < 100)
    at_cap = first_round(chain, salt, "0.97", DEFAULT_CAP, lambda x, c: c == 1_000_000)
    small_cap = first_round(chain, salt, "0.97", "2.00", lambda x, c: raw_hundredths(x, "0.97") > 500)
    large = first_round(chain, salt, "0.97", DEFAULT_CAP, lambda x, c: 10_000 < c < 1_000_000)
    floor_case = first_round(
        chain, salt, "0.95", DEFAULT_CAP,
        lambda x, c: 100 < c < 200 and x not in first_three and (100 * 95 * E) % (100 * (E - x)) != 0)

    vectors = [
        vector(chain, salt, 1, "0.97", DEFAULT_CAP, "round 1 of the chain, demo RTP 0.97"),
        vector(chain, salt, 2, "0.99", DEFAULT_CAP, "round 2, highest allowed RTP 0.99"),
        vector(chain, salt, 3, "0.90", DEFAULT_CAP, "round 3, lowest allowed RTP 0.90"),
        vector(chain, salt, instant, "0.97", DEFAULT_CAP, "instant crash: the formula gives less than 1.00, result 1.00"),
        vector(chain, salt, at_cap, "0.97", DEFAULT_CAP, "the formula reaches the default cap, result 10000.00"),
        vector(chain, salt, small_cap, "0.97", "2.00", "uncapped value above 5.00 limited by a 2.00 cap"),
        vector(chain, salt, large, "0.97", DEFAULT_CAP, "large multiplier (above 100.00) below the cap"),
        vector(chain, salt, floor_case, "0.95", DEFAULT_CAP, "non-integer quotient: floor to hundredths, RTP 0.95"),
    ]
    assert any(v["crashPoint"] == "1.00" for v in vectors)
    assert any(v["crashPoint"] == v["cap"] for v in vectors)

    return {
        "$schema": "./fairness-v1.schema.json",
        "$comment": "Generated by tests/vectors/generate_fairness_v1.py (independent reference of SPEC-0007 §4). "
                    "Do not edit by hand. Algorithm v1 is frozen: a change needs a spec amendment and a new version.",
        "version": 1,
        "algorithm": {
            "chain": "h_0 = 32 random bytes; h_{k+1} = SHA256(h_k) over the raw 32-byte digest; round r uses "
                     "seed s_r = h_{N-r}; previousSeed s_{r-1} = SHA256(s_r), with s_0 = h_N (terminal hash).",
            "hmac": "H = HMAC-SHA256(key = the 32 raw bytes of seed, message = the raw bytes of salt (hex-decoded, "
                    "not the UTF-8 text of the hex)).",
            "sample": "X = the first 52 bits of H, big-endian unsigned integer (the first 13 hex digits of H); E = 2^52.",
            "crashPoint": "min(cap, max(1.00, floor(100 * RTP * E / (E - X)) / 100)) in exact integer arithmetic, "
                          "RTP as an exact decimal (0.97 = 97/100).",
            "encoding": "seed, previousSeed and salt: lowercase hex of the raw bytes; rtp, cap and crashPoint: decimal "
                        "strings with '.' as separator (cap and crashPoint with exactly 2 decimals).",
        },
        "vectors": vectors,
    }


def render() -> str:
    return json.dumps(build_document(), indent=2, ensure_ascii=False) + "\n"


def main() -> int:
    content = render()
    if "--check" in sys.argv[1:]:
        if OUTPUT.read_text(encoding="utf-8") != content:
            print(f"{OUTPUT.name} differs from the reference computation", file=sys.stderr)
            return 1
        return 0
    OUTPUT.write_text(content, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
