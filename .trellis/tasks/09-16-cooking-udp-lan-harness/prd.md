# Cooking UDP LAN minimum vertical slice

## Goal

Implement a pure .NET LiteNetLib reliable-UDP listen-host/client slice for the Cooking application. The host must accept a remote client while its local player and the remote player use the same existing `CookingSessionAuthority` command queue and authority path. Deliver protocol contracts, real UDP loopback, a same-machine multi-process harness, and a documented two-physical-PC LAN acceptance procedure.

## Scope

- A Cooking-specific UDP protocol and LiteNetLib host/client adapter in new pure .NET projects.
- Versioned datagrams for handshake, authority-bound identity, commands/results, baseline/delta state transfer, resynchronization and connection loss.
- Full canonical `CookingSnapshot` payloads in baseline and delta messages, with client-side hash verification.
- A serialized authority dispatcher: UDP callbacks only copy/decode/enqueue immutable work.
- Automated codec, lifecycle, thread-isolation and loopback tests.
- Host/client console harness, same-machine runner, structured artifacts, and topology-aware diagnostics.
- Measurement metadata that distinguishes loopback, same-machine and two-PC LAN evidence while performance thresholds remain `UNSET`.
- A manual `cooking-udp` P1 test gate and matching documentation.

## Non-goals

- Cooking Unity packages, scenes, UI, authoring, projection or EditMode.
- Orleans gateway/grain integration or changes to generic AbilityKit network packages.
- Automatic reconnect, endpoint rebinding, host migration, NAT traversal, WAN/relay, authentication/encryption, host-exit/save semantics, interpolation, prediction or rollback.
- Claiming two-PC LAN acceptance from loopback or same-machine results.

## Constraints

- Do not create a local branch.
- Keep `CookingSessionAuthority` calls serialized; LiteNetLib unsynchronized callbacks must never mutate authority state.
- Use LiteNetLib 2.1.4 `ReliableOrdered` as this task's UDP candidate. Its connection key is a test connectivity gate, not authentication.
- Preserve existing P1/P6 authority and in-process measurement evidence; this task is a new successor and does not reopen archived tasks.
- Do not modify host firewall or network configuration.

## Acceptance criteria

1. A remote UDP client completes a compatible handshake, receives an authority-assigned player binding and a full baseline snapshot.
2. Host-local and remote commands share one authority dispatcher, and command results plus snapshots converge on the same canonical state hash.
3. Malformed, oversized, unknown-version, incompatible, stale, duplicate and out-of-sequence messages are rejected without authority mutation and produce correlated diagnostics.
4. UDP callback tests prove that authority work is dispatched rather than run on the callback thread.
5. A real socket loopback test passes, and a same-machine two-process runner produces bounded, topology-labelled host/client artifacts.
6. The two-PC procedure records endpoint, NIC, firewall, workload, protocol/config identity and state/trace evidence; it remains not-run until physical devices execute it.
7. Performance targets remain `UNSET`; no synchronization optimization is enabled or claimed.
