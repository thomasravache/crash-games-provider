# Provably-fair vectors v1 (SPEC-0007)

`fairness-v1.json` is contract v1 of the Fairness module (SPEC-0007 §6, ADR-0006): the bare JSON array
`[{ seed, previousSeed, salt, rtp, cap, crashPoint }]`. The C# Fairness tests (UT-01, CT-01), the round engine
(SPEC-0008) and the TypeScript verifier (SPEC-0010) all use it. Each vector may also carry an optional `description`.
`fairness-v1.schema.json` is its JSON Schema.

The file is generated. Do not edit it by hand:

```bash
python3 tests/vectors/generate_fairness_v1.py           # rewrite fairness-v1.json
python3 tests/vectors/generate_fairness_v1.py --check   # exit 1 if the file differs from the reference computation
```

The generator is the independent reference of SPEC-0007 §4. It uses only the Python standard library and shares no
code with the C# module or the TypeScript verifier. Algorithm v1 is frozen: changing it needs a spec amendment, a new
version and a new certification.

## Algorithm v1

1. **Chain:** `h_0` = 32 random bytes; `h_{k+1} = SHA256(h_k)`, hashing the raw 32-byte digest (never its hex text).
   Round `r` (1..N) uses seed `s_r = h_{N-r}`. Its previous seed is `s_{r-1} = SHA256(s_r)`, and `s_0 = h_N` is the
   terminal hash.
2. **HMAC:** `H = HMAC-SHA256(key = the 32 raw bytes of the seed, message = the raw bytes of the salt)`. The salt
   bytes are the hex-decoded salt, not the UTF-8 text of its hex.
3. **Sample:** `X` = the first 52 bits of `H`, read big-endian as an unsigned integer (the first 13 hex digits of
   `H`). `E = 2^52`.
4. **Crash point:** `crash = min(cap, max(1.00, floor(100 · RTP · E / (E − X)) / 100))`, in exact integer arithmetic.
   RTP is an exact decimal (0.97 = 97/100), so the value in hundredths is `floor(100 · 97 · E / (100 · (E − X)))`.

## Encoding

- `seed`, `previousSeed`, `salt`: lowercase hex of the raw bytes.
- `rtp`, `cap`, `crashPoint`: decimal strings with `.` as the separator. `cap` and `crashPoint` always have exactly
  2 decimals.

## Fixed inputs of the vectors

- Chain origin: `SHA256("apogeu/fairness-v1/vectors/origin")`, with N = 100,000.
- Salt: `SHA256("apogeu/fairness-v1/vectors/salt")`.
- The file includes a vector where the formula gives less than 1.00 (result `1.00`) and one that reaches the
  `10000.00` cap.
