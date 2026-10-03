# Candidate O01 wire list serialization cost increment

2026-10-03 ROOT DESIGN ONLY. Actual first eligible-candidate Release2x5 numbers exceed terminalp95 target; full provenance reconciliation/four baseline profiles remain underway. This candidate is not yet implemented or attributed as the dominant runtime cost. No production edit during frozen Release9093.

## Existing source and bounded proposal

CookingNetworkWireCodec.ReadOnlyListConverter<T>.Write currently materializes value.ToArray then serializes that array for every nested read-only list. Candidate replacement writes StartArray, serializes each existing element with the exact same JsonOptions, then EndArray. This avoids one temporary list array per write without changing source graph, model, property ordering, enum/escaping/null behavior or public options. Read converter, Encode complete bounds validation, TryDecode scanner/duplicate rejection, all frame/token/list/depth defaults, transport owned byte arrays and authority/publication remain unchanged. No pooling, compression, rate changes, delta snapshots or history pruning.

## Mandatory proof before acceptance

Establish legacy comparator with its original list converter in a new test-only serializer; compare exact UTF8 bytes for supported typed baseline/capture/commands/results, nested empty/nonempty lists, null elements/null collections, enums/escaping/non-ASCII values and realistic growing receipt/tick-history graphs. Recompute actual public full/combined hashes and compare decode results; current over-limit/duplicate/unsupported schema/nullability failures must retain outcomes. Frozen earlier snapshots remain unchanged after later mutable-source operations. Meaningful allocation probe over the same retained typed full graph may demonstrate array allocation removal but cannot claim end-to-end performance improvement.

Root must review baseline eligibility and exact new test/source ownership, then grant an isolated managed worktree for implementation. Production ownership would be ONLY codec list writer plus new meaningful codec equivalence tests and task/report. No worker may edit generic LiteNet or gameplay ticks under this grant. Applicable Cooking/ET/network gates and three fresh matched ordinary2/4x5 profiles on identical hardware/build configuration are required; no measured limit may be reduced. If this increment does not close performance, preserve evidence and continue profiling other actual serialization/publication/copy costs. This is one bounded candidate, not a replacement for remaining raw impairment/rich/physical exits.
