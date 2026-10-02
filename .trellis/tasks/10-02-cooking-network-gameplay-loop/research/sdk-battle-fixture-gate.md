# SDK Battle transport contract fixture gate investigation

2026-10-03, natural_operating_implement. Ownership: NetworkTransportContractTests.cs only plus this report; production and root SampleNetworkArchitectureTests.cs untouched.

Observed root network-sdk gate: fire-and-forget Assert.Single saw no send, and awaitable input tests stalled. Static cause is the old fixture never called NetworkTransport.Connect, and fake Open never emitted Connected. NetworkTransport.SendInput deliberately waits for authentication completion; construction of an already-connected-looking fake does not establish that lifecycle. NetworkTransport.OnConnected starts authentication and completes it immediately when auth opcodes are unconfigured.

Minimal correction: fake Open emits Connected; NewTransport calls the real Connect and asserts IsAuthenticated before exercising the data plane. No response, retry, payload, ordering, or count assertion was removed. No auth gate bypass or production change. Existing fixture options have zero auth opcodes, so authentication adds no outbound packet and existing counts retain their original meaning.

Static git diff --check passed. Actual focused execution pending root serial .NET window. Requested command: dotnet test src/AbilityKit.Network.Battle.Tests/AbilityKit.Network.Battle.Tests.csproj --filter FullyQualifiedName~NetworkTransportContractTests --logger trx with retained log/results; then full Battle project and root network-sdk gate. Root owns gate cancellation: interrupt only its owned current exec session/child process tree, preserve aborted log, never globally terminate dotnet processes.

## Actual verification and frozen result

Root explicitly interrupted the original owned network-sdk gate using Ctrl+C; the partial failed/aborted run remains evidence, not a passed gate. Root then granted the serialized .NET window.

Actual focused command: dotnet test src/AbilityKit.Network.Battle.Tests/AbilityKit.Network.Battle.Tests.csproj --filter FullyQualifiedName~NetworkTransportContractTests --logger "trx;LogFileName=battle-fixture-focused.trx" --results-directory local/Logs/network-battle-fixture --verbosity minimal. Result: 10 passed, 0 failed/skipped, 39 ms; log local-battle-fixture-focused.log, matching retained TRX.

Actual full command: powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate network-sdk. Exit 0, gate passed 40.4 seconds. Retained stdout local-network-sdk-fixture-fixed.log and summary/TRX directory local/Logs/test-gates/20261003-035826-network-sdk/network-sdk. All 12 actual TRX files independently summed: 311 passed, zero failed/notExecuted. Per-step counts: SDK146, Host19, HostIntegration8, MobaAdapter6, ShooterAdapter3, Room76, Battle18, InMemory1, LiteNet18, WebSocket1, BattleConfig12, Client3. This assembled-tree gate includes root-owned SampleNetworkArchitectureTests.cs correction; it is not attributed solely to this fixture change. Existing nullable/unused-event warnings remain, no new production fix.

.NET window explicitly released to root after process exit. Only owned test/report are committed; root process acceptance and architecture test files are left untouched/uncommitted by this worker. No Cooking gameplay/physical LAN/Unity completion follows from this SDK gate.
