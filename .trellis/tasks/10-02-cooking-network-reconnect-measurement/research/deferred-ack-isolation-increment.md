# Deferred ACK isolation increment

Source prepared 2026-10-03; execution NOT_RUN pending root serialized .NET window. Owned new `CookingNetworkDeferredAckIsolationTests.cs` only. This worker is not alone in the recovery tree and has not changed or staged other work.

Four theory cases create actual ET Running kitchen, Pause, close participant A to create CleanupPending, join B, retain exact issued ACK twice while paused, and reject wrong issue identity without replacing it. They then exercise B close/rebind, live valid B supersession, actual public trusted no-Front BeginEnd(Success)/CompleteEnd/CreateSuccessor scope retirement, or public Host.Dispose authority-unavailable reset. No reflection, fake authority, manual identity injection into Session fields or illegal simultaneous active participant bindings is used. Current scope/generation exact ACK is required before new Ready; old retained correlation must not leak. Small no-Front trusted lifecycle controls are not natural rich service evidence.

Actual Faulted allocator branch remains separate from Disposed; preparation is not execution proof. Counts/results will be appended only after actual command completion. No broader master, physical LAN or performance acceptance is claimed.

Source now includes fifth `faulted` theory: trusted factory seeds an actual raw item at its permitted station with public ICookingProductIdAllocator throwing IOException on product allocation. Trusted StartProcess before Pause advances actual progress to1/required2. After the same genuine pending-cleanup deferred ACK setup, Resume's next owner frame must complete processing and fault the real Host; Session must emit no retained Ready and keep all participants unready. Dynamic trusted command batch is HostFrameSequence+1. This remains NOT_RUN until serialized execution is granted.

## Actual focused execution

Root granted serialized .NET window after rich final execution released it. First command compiled successfully and executed5: passed4/failed1/skipped0, exit1. The scope fixture attempted BeginEnd(Success) directly from Paused, which public lifecycle correctly rejects; this is fixture red, not a production defect. Preserved `local/deferred-ack-first.log` and `local/Logs/cooking-execution/network-deferred-ack/deferred-first.trx`.

Fixed only scope fixture to public Host.Resume, BeginEnd(Success), CompleteEnd and CreateSuccessor consecutively without intervening Session owner frame. Thus the genuinely created DeferredAck persists until Session observes real successor scope retirement. No private mutation or bypassed lifecycle guard.

Second actual command: `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingNetworkDeferredAckIsolationTests --logger "trx;LogFileName=deferred-second.trx" --results-directory local/Logs/cooking-execution/network-deferred-ack --verbosity minimal`. Exit0, passed5/failed0/skipped0,397ms test duration. `local/deferred-ack-second.log` and matching TRX retained. This covers actual close/rebind, supersession, successor retirement, Disposed and allocator Faulted after a real deferred exact ACK, plus invalid issue ID before each transition. .NET window released to root; runner not built/run. Broader master acceptance remains root-owned.
