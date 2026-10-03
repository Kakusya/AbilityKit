# Native cadence application source map - proposed decision only

2026-10-03 readonly delegated source audit on main07458cd6d, recorded by root. No production adoption, default change or new execution scope follows from this document. O03 Profile81315 is active; outcomes must be reviewed first.

Generic shared UPM source remains in Unity/Packages/com.abilitykit.network.transport.litenet/Runtime/, reused by SDK Compile Include. Isolated immutable LiteNetTransportOptions defaults15 and rejects nonpositive values; old constructor ABI retained. New overloads allow explicit client(key,options) and listener(address,port,key,capacity,options). Both actual managers must receive the same selected option. Owner10ms scheduling/fixed business Tick, wire, bounds and full history remain unchanged.

| Application path under src | Actual injection points |
|---|---|
| AbilityKit.Game.Cooking/Session/CookingNetworkSessionClient.cs | Default factory line32 remains15; existing Func<ITransport> allows opt-in without public constructor change. |
| AbilityKit.Game.Cooking/Session/CookingSessionClient.cs | Legacy private CreateTransport line34 has no such injection; preserve legacy default, no claim of adopting1. |
| AbilityKit.Game.Cooking.NetworkAcceptance/Program.cs | Listener61, remote client204 defaultfactory. |
| AbilityKit.Game.Cooking.NetworkMeasurement/Program.cs | Listener49, UDP clients54 defaultfactory; InProcess local factory separate. |
| AbilityKit.Game.Cooking.NetworkProcessMeasurement | Program listener36; FramedFaultPeer remote factory10. |
| AbilityKit.Game.Cooking.NetworkImpairmentMeasurement/Program.cs | Listener23; Compile Include reuses preceding FramedFaultPeer, must coordinate both. |
| AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance/RichRunner.cs | Listener251, remote factory294. |
| AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance/Program.cs | Listener63, remote factory146. |
| Cooking tests | CookingNetworkSessionV3Tests69/81, CookingLiteNetFramingBoundaryTests listener/client controls. |

The narrow prospective application-owned proposal is explicit options on approved runner listener and injected remote factory, with generic/default v3/legacy factories15 retained. A tools-only configuration must be labelled as that selected application configuration; do not claim unmodified default clients adopted it. Generic tests/README examples keep validating old/default15 paths.

After any reviewed actual adoption: merge-source generic7/default25/oldconsumer ABI and affected SDK proof; changed runner typed complete graph/ACK/Ready/reconnect/close controls; rich4 and its verifier-specific negatives; affected concurrency21 configuration two real pairs; current-source impairment prerequisites/P6; matched ordinary Release12. Existing unaffected singleplayer and InProcess semantic evidence retain their exact source/scope rather than blanket reruns. No performance pass or physical/Unity inference from this map.
