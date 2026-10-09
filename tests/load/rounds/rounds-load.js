// Load test of the rounds hub (SPEC-0008 §7.6, ADR-0019): 1,000 SignalR connections in one room for 10 rounds.
// Pass criteria (thresholds fail the run): RoundCrashed delivered to every connection with p99 <= 200 ms, measured from the
// server's crashedAt, and zero unintended disconnections.
//
// Runs on demand against the local Aspire environment with the official k6 image pinned by digest (no local install), using
// a real player session of SPEC-0009 (the hub requires an authenticated user). The session is a stateless JWT bound to the
// demo sala, so every VU reuses the same token: one session is enough and the per-IP limit of POST /api/v1/sessions is
// never hit.
//
//   dotnet run --project src/backend/AppHost --launch-profile http        # Development: maps /dev/launch-demo
//   BASE_URL=http://localhost:<api-port>                                  # the "api" http endpoint in the Aspire dashboard
//   LT=$(curl -s -o /dev/null -w '%{redirect_url}' "$BASE_URL/dev/launch-demo" | sed -E 's/.*[?&]lt=([^&]+).*/\1/')
//   SESSION_TOKEN=$(curl -s -X POST "$BASE_URL/api/v1/sessions" -H 'content-type: application/json' \
//     -d "{\"launchToken\":\"$LT\"}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["sessionToken"])')
//   docker run --rm -v "$PWD/tests/load/rounds:/scripts:ro" \
//     -e BASE_URL="${BASE_URL/localhost/host.docker.internal}" -e SESSION_TOKEN="$SESSION_TOKEN" -e SALA_ID=demo \
//     "$K6_IMAGE" run /scripts/rounds-load.js
//
//   K6_IMAGE=grafana/k6:2.3.0@sha256:9c2dee7f8ed74d317e4027c06a10f169b625638189de8d4555d0b3486a5aeb34
//
// CONNECTIONS and ROUNDS override the 1,000 connections and 10 rounds (e.g. a smoke run with CONNECTIONS=10 ROUNDS=1).
// The result (k6 summary) is recorded in the Delivery Report of SPEC-0008.
import ws from "k6/ws";
import { Counter, Trend } from "k6/metrics";

const baseUrl = (__ENV.BASE_URL || "http://localhost:5000").replace(/\/$/, "");
const sessionToken = __ENV.SESSION_TOKEN;
const salaId = __ENV.SALA_ID || "demo";
const connections = Number(__ENV.CONNECTIONS || 1000);
const rounds = Number(__ENV.ROUNDS || 10);

const recordSeparator = "\u001e";

const crashDelivery = new Trend("round_crashed_delivery_ms", true);
const crashesObserved = new Counter("round_crashed_observed");
const unexpectedDisconnects = new Counter("unexpected_disconnects");
const joinFailures = new Counter("join_sala_failures");

export const options = {
  scenarios: {
    sala: {
      executor: "per-vu-iterations",
      vus: connections,
      iterations: 1,
      maxDuration: "40m",
    },
  },
  thresholds: {
    round_crashed_delivery_ms: ["p(99)<=200"],
    unexpected_disconnects: ["count==0"],
    join_sala_failures: ["count==0"],
    round_crashed_observed: [`count>=${connections * rounds}`],
  },
};

export function setup() {
  if (!sessionToken) {
    throw new Error(
      "SESSION_TOKEN is required: a player session of SPEC-0009 (the hub requires an authenticated user).",
    );
  }
}

function frame(message) {
  return JSON.stringify(message) + recordSeparator;
}

export default function () {
  const url = `${baseUrl.replace(/^http/, "ws")}/hubs/rounds?access_token=${encodeURIComponent(sessionToken)}`;
  let crashes = 0;
  let done = false;

  const response = ws.connect(url, {}, (socket) => {
    socket.on("open", () => {
      socket.send(frame({ protocol: "json", version: 1 }));
    });

    socket.setInterval(() => socket.send(frame({ type: 6 })), 15000);

    socket.on("message", (data) => {
      for (const raw of data.split(recordSeparator)) {
        if (raw.length === 0) {
          continue;
        }
        const message = JSON.parse(raw);
        if (message.type === undefined) {
          // Handshake answer: join the room.
          if (message.error) {
            joinFailures.add(1);
            socket.close();
            return;
          }
          socket.send(
            frame({ type: 1, invocationId: "join", target: "JoinSala", arguments: [salaId] }),
          );
        } else if (message.type === 3 && message.invocationId === "join" && message.error) {
          joinFailures.add(1);
          socket.close();
        } else if (message.type === 1 && message.target === "RoundCrashed") {
          const crashed = message.arguments[0];
          crashDelivery.add(Date.now() - Date.parse(crashed.crashedAt));
          crashesObserved.add(1);
          crashes += 1;
          if (crashes >= rounds) {
            done = true;
            socket.close();
          }
        }
        // A server close (type 7) is counted by the "close" handler below.
      }
    });

    socket.on("close", () => {
      if (!done) {
        unexpectedDisconnects.add(1);
      }
    });

    socket.on("error", () => {
      if (!done) {
        unexpectedDisconnects.add(1);
      }
    });
  });

  if (!response || response.status !== 101) {
    unexpectedDisconnects.add(1);
  }
}
