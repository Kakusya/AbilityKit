# O02 typed outbound envelope cost candidate

2026-10-03 root reviewed source proposal; source implementation/testing not started. O01b focused proof is narrow and broad gates still live; all ordinary reference configurations remain NOT_ACCEPTED. This candidate does not assert dominant cause or replace P0-P6, rich recovery, physical LAN or matched performance exits.

## Source-backed bounded hypothesis

Current WireCodec.Encode serializes payload to JsonElement, serializes CookingNetworkWireEnvelope containing that element, then performs complete TryDecode validation. A private generic outbound envelope with identical ordered ProtocolVersion/Kind/CorrelationId/Payload properties could serialize the typed payload once with the same JsonOptions, avoiding the intermediate document. It MUST retain the entire final TryDecode scanner/deserializer call. No validation bypass, history pruning, delta snapshots, pooling, new transport defaults or authority changes are included.

## Mandatory compatibility before source acceptance

Keep an explicit original test-only encoder using SerializeToElement and original envelope. Compare exact UTF8 bytes on actual full typed ET baseline/capture/commands/results, complete growing history, null payloads/lists/elements, enum/non-ASCII/escaping, supported generic interface/runtime payload types and immutable earlier captures. Public Hash/BaselineHash and all supported wire fields remain unchanged. Retain all existing collection/token/frame/control/command bounds and duplicate/nullability outcomes.

Nested serialization may change depth accounting or exception type; tests must cover accepted/rejected near32 depth and exact externally observed exception families. Publish catches ArgumentException for wire bounds, so introducing uncaught JsonException on previously classified bounds is a regression, not an acceptable shortcut. Public JsonOptions behavior must not be replaced by hardcoded field names/options. This is a hypothesis pending real comparison, not a claim that every generic payload is compatible.

## Execution and evidence boundary

Root may assign source exploration only in an isolated checkout AFTER its current frozen gate/profile use terminates; no writes to a tree being measured. Exact approved ownership: codec private outbound representation/Encode only, new meaningful equivalence controls, task report. Broader source changes require renewed scoped review. Record old/new allocation and timing over the same retained graph after warmup, then applicable Cooking/ET/network gates and three fresh matched ordinary2/4x5 Release configurations on the reference machine. Microprobe gain cannot replace end-to-end latency/projected-throughput acceptance. Negative candidates remain archived and must not be imported as successful optimizations.