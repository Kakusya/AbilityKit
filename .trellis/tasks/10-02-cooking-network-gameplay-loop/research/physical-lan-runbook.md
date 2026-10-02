# Physical two-PC acceptance handoff

Current status: **NOT_VERIFIED**. A second physical PC/its endpoint is unavailable in this session. Same-machine UDP, distinct processes, InProcess, cold restore and benchmark success do not satisfy this exit.

Use the same verified NetworkAcceptance build on both machines; copying identical DLL/deps/runtimeconfig/content files prevents unrelated build-MVID differences. Keep its source provenance and executable/file hashes. The executable is framework-dependent net10.0 and requires the compatible .NET runtime, as specified by its runtimeconfig. Do not assume the second PC has it.

For a checkout, the existing wrapper supports:

```powershell
powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-process-acceptance.ps1 -Mode Host -BindIp 0.0.0.0 -Port 18090
powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-process-acceptance.ps1 -Mode Client -RemoteIp '实际Host的局域网IPv4' -Port 18090
```

Run Host on the first physical PC and Client on the second. Client starts after Host's actual `READY` line, not merely after launching the process. Replace the address placeholder with the actual first PC address. The Host/Client modes retain topology `SeparateHostsRequiresPairedEvidence`; neither mode alone certifies that the operator used physical machines. Preserve both endpoint reports, stdout/stderr, effective addresses, machine/physical topology record, PIDs, source/build hashes and local exit codes.

The tested scenario includes finite procurement, interrupted manual work taken over by the partner, unattended processing/shared portions, independently bound completed drink, two submitted orders, one natural unmet departure, cleanup and zero-star Success, actual durable successor and generation/token rebind in Created. Preserve all checks from both reports. Compare exact complete gameplay hash, same instance/scope and matching nonempty valid ET/Session MVIDs. Each received combined baseline hash is checked by the executable; final Session views may differ after the Client exits, so do not demand final Ready/Connected flags or combined hashes be identical. Do not compare timestamps across machines.

A full physical exit also needs the N02 concurrency/rejection controls: same item pickup and slot placement arbitration, simultaneous legal independent work, incompatible input rejection without item loss, stale scope/identity rejection and recovery after disconnect. Existing focused same-machine tests remain their own evidence; explicitly record which controls were or were not exercised on physical LAN. No pass from absence of errors or matching hostname alone.

Keep performanceTargetUNSET unless a workload/threshold is separately approved. A connection failure or timeout is a failed/blocked run with its artifacts retained, not successful scope completion. Owner authorization does not supply a second machine, network address or executed proof. N02 stays in_progress until actual paired physical evidence satisfies its exit; N03 and Unity are not automatically completed by it.
