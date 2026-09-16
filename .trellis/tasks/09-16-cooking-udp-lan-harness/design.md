# Design: Cooking UDP LAN minimum vertical slice

## Decisions

- Add a Cooking-owned pure .NET UDP boundary rather than changing generic AbilityKit networking or reusing Orleans.
- Use LiteNetLib 2.1.4 `ReliableOrdered`. LiteNetLib owns reliable datagram delivery for this slice; the Cooking layer retains command identity deduplication and authority sequence checks.
- Each LiteNet payload contains one complete versioned Cooking wire envelope. Datagram boundaries are message boundaries; no stream segmentation/coalescing behavior is implied.
- The host assigns `ConnectionId` and `PlayerId`. A client-provided command player is validated by `CookingSessionAuthority` and cannot override the binding.
- The adapter owns one single-reader `Channel` dispatcher. LiteNet callbacks decode to immutable envelopes and enqueue them; the dispatcher is the only code that invokes `CookingSessionAuthority`.
- The host creates complete snapshot messages after accepted batches. The adapter client installs full snapshots only after metadata and canonical hash verification.

## Data flow

```text
UDP callback -> datagram decode/validation -> immutable inbound queue
             -> single authority dispatcher -> CookingSessionAuthority
             -> command result + full snapshot -> datagram codec -> LiteNet send
```

Host-local input enters the same queue through a local connection record. Client application state is a snapshot projection only; it has no authority reference.

## Wire contract

`CookingUdpEnvelope` owns schema (`abilitykit.cooking.udp.v1`), protocol name/version, kind, correlation ID, scope, epoch, config identity and a JSON payload. Kinds are handshake request/accepted/rejected, baseline, command, command result, delta, synchronization rejected and transport closed.

The codec enforces nonblank identity fields, a single size limit, known schema/kind, and type-specific payload decoding. It maps transport input to Cooking domain values only in the adapter. A baseline/delta carries a full `CookingSnapshot`, its sequence/baseline reference and its canonical hash. The receiving projection checks hash and only accepts compatible sequence transitions.

## Lifecycle and failure semantics

- Host connection request validates the configured LiteNet connection key.
- Connection callbacks allocate monotonic transport connection IDs and post lifecycle work to the dispatcher.
- Decode errors and malformed envelopes get a diagnostic/rejection response if the peer is known; no authority call occurs.
- A disconnect posts `ReportTransportLoss`; dispatcher disposal makes remaining inbound work reject safely.
- Stop cancels receive/dispatch loops, closes peers, and is idempotent. Restart must use a new bound socket and remain testable.
- No reconnect/rebinding/migration policy is introduced.

## Testing layers

1. Codec contract tests: deterministic encoding, malformed/oversized/unknown rejection, payload round trip.
2. Adapter tests: handshake binding, full baseline/delta projection, command dedup, stale sequence and dispatcher thread isolation.
3. LiteNet loopback tests: key validation, full host/client flow, stop/restart and loss.
4. Harness tests: host/client process lifecycle and artifact validation on localhost.
5. Two-PC LAN: manual evidence exit, separate from automated test claims.

## Measurement

Topology and endpoint metadata will distinguish loopback UDP, same-machine UDP and two-PC LAN UDP. Transport encode/send, receive/decode, projection apply and authority ingress-to-commit are separate metrics. Existing `UNSET` thresholds and optimization blockers remain unchanged.
