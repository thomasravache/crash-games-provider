// Provably fair verifier in the browser (SPEC-0010 §4, ADR-0006): algorithm v1 of SPEC-0007@2
// (tests/vectors/README.md) in exact integer arithmetic (BigInt), tested with the same vectors as the C# module.
// Hashing uses Web Crypto (browser and Node), so nothing here depends on the transport.

/** Data of one finished round (GET /api/v1/rounds/{roundId}/verification, vectors of SPEC-0007@2). */
export interface RoundVerificationData {
  /** Revealed seed s_r, lowercase hex of 32 bytes. */
  readonly seed: string;
  /** s_{r-1} = SHA256(s_r), lowercase hex. */
  readonly previousSeed: string;
  /** Chain salt, lowercase hex of its raw bytes. */
  readonly salt: string;
  /** RTP as a decimal string ("0.97"). */
  readonly rtp: string;
  /** Cap as a decimal string with 2 places ("10000.00"). */
  readonly cap: string;
  /** Crash point the server claims, decimal string with 2 places. */
  readonly crashPoint: string;
}

export type VerificationResult = "valid" | "chain-mismatch" | "crash-mismatch";

/** E = 2^52: the sample X is the first 52 bits of the HMAC. */
const sampleSpace = 1n << 52n;

function hexToBytes(hex: string): Uint8Array<ArrayBuffer> {
  if (hex.length % 2 !== 0 || !/^[0-9a-f]*$/iu.test(hex)) {
    throw new RangeError("Expected an even-length hexadecimal string.");
  }
  const bytes = new Uint8Array(hex.length / 2);
  for (let index = 0; index < bytes.length; index += 1) {
    bytes[index] = Number.parseInt(hex.slice(index * 2, index * 2 + 2), 16);
  }
  return bytes;
}

function bytesToHex(bytes: ArrayBuffer): string {
  return Array.from(new Uint8Array(bytes), (byte) => byte.toString(16).padStart(2, "0")).join("");
}

/** Exact value of a decimal string as numerator / denominator ("0.97" → 97 / 100). */
function parseDecimal(text: string): { numerator: bigint; denominator: bigint } {
  const match = /^(\d+)(?:\.(\d+))?$/u.exec(text.trim());
  if (match === null) throw new RangeError(`Not a decimal number: "${text}".`);
  const integer = match[1] ?? "0";
  const fraction = match[2] ?? "";
  return {
    numerator: BigInt(integer + fraction),
    denominator: 10n ** BigInt(fraction.length),
  };
}

/** Value in hundredths, rounded down (cap and crash point have exactly 2 places). */
function toHundredths(text: string): bigint {
  const { numerator, denominator } = parseDecimal(text);
  return (numerator * 100n) / denominator;
}

function formatHundredths(hundredths: bigint): string {
  const text = hundredths.toString().padStart(3, "0");
  return `${text.slice(0, -2)}.${text.slice(-2)}`;
}

function subtle(): SubtleCrypto {
  const crypto = (globalThis as { crypto?: Crypto }).crypto;
  if (crypto?.subtle === undefined) {
    throw new Error("Web Crypto (crypto.subtle) is required to verify a round.");
  }
  return crypto.subtle;
}

async function hmacSha256(key: Uint8Array<ArrayBuffer>, message: Uint8Array<ArrayBuffer>) {
  const cryptoKey = await subtle().importKey("raw", key, { name: "HMAC", hash: "SHA-256" }, false, [
    "sign",
  ]);
  return subtle().sign("HMAC", cryptoKey, message);
}

/** Crash point of algorithm v1 as a decimal string with 2 places. */
export async function computeCrashPoint(
  input: Pick<RoundVerificationData, "seed" | "salt" | "rtp" | "cap">,
): Promise<string> {
  const digest = bytesToHex(await hmacSha256(hexToBytes(input.seed), hexToBytes(input.salt)));
  const sample = BigInt(`0x${digest.slice(0, 13)}`);
  const rtp = parseDecimal(input.rtp);
  // floor(100 · RTP · E / (E − X)) in hundredths, with RTP = numerator / denominator.
  const raw = (100n * rtp.numerator * sampleSpace) / (rtp.denominator * (sampleSpace - sample));
  const cap = toHundredths(input.cap);
  const hundredths = raw < 100n ? 100n : raw > cap ? cap : raw;
  return formatHundredths(hundredths);
}

/** valid | chain-mismatch (SHA256(seed) ≠ previousSeed) | crash-mismatch (recomputed crash ≠ claimed crash). */
export async function verifyRound(data: RoundVerificationData): Promise<VerificationResult> {
  const previous = bytesToHex(await subtle().digest("SHA-256", hexToBytes(data.seed)));
  if (previous !== data.previousSeed.toLowerCase()) return "chain-mismatch";
  const recomputed = await computeCrashPoint(data);
  return toHundredths(recomputed) === toHundredths(data.crashPoint) ? "valid" : "crash-mismatch";
}
