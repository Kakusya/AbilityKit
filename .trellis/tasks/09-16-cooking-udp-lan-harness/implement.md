# Implementation checklist: Cooking UDP LAN minimum vertical slice

1. Add task context manifests and start the approved task.
2. Create pure .NET Cooking UDP library and test/harness projects with LiteNetLib 2.1.4 references.
3. Implement wire envelope, strict codec and snapshot projection; test malformed/oversized/version/payload rejection.
4. Implement LiteNet host/client and single-reader host dispatcher; map handshake/command/baseline/delta/loss to the existing authority.
5. Add real UDP loopback tests proving host-local plus remote authority behavior, lifecycle and callback thread isolation.
6. Add a host/client console harness and same-machine runner with bounded waits, owned-process cleanup, topology-labelled JSONL/report artifacts.
7. Extend measurement topology/metric metadata without setting performance thresholds or enabling optimizations.
8. Add a manual P1 `cooking-udp` gate and update the gate documentation.
9. Run focused projects, gate and same-machine harness; record actual evidence. Mark physical two-PC LAN as not-run unless executed on two devices.
