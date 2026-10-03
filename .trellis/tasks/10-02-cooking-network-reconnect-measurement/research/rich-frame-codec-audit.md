# Rich application framing correction (2026-10-03)

Source correction: root caught676eb20fe padded Join fixture before execution. Final kind-specific caps are Join ControlBytes4096, CommandBytes16384 and Baseline FrameBytes8388608. Corrected frame-controls max-wire fixture uses Baseline-kind synthetic parser payload, not a typed business baseline; original Join description below is historical source error, not valid8MiB control admission. No budget changed and no original control pass was claimed.

Status: root-approved runner-only source preparation; compilation, controls and actual rich rerun PENDING. No .NET executed by this owner. Generic production, Cooking wire/token/collection/history/time budgets remain unchanged.

## Original actual failure and source cause

Root rich43003 exited1 in first manual-paused. Retained evidence: `local/Logs/cooking-network-rich-recovery/20261003-085558-0845307/manual-paused`. Host stderr reports `Frame too large: 4194973` in NetworkFrameReader47 through default LengthPrefixedFrameCodec decoder and RichPeer.Poll. Host local Chef uses CreateLocalClientTransport and fails during procurement/preparation. This is not evidence of exceeding the Cooking8MiB wire budget.

RichPeer supplied ConnectionOptions.MaxFrameLength=8MiB+64, but ConnectionManager only consumes its FrameCodec injection seam; the generic default singleton constructs an unconfigured4MiB reader. Same RichPeer code is used by local and remote participants. Cooking production host/client already explicitly inject internal CookingNetworkFrameCodec(FrameBytes+64).

Advertised frame body includes16-byte NetworkPacketHeader and excludes4-byte prefix. Original body4194973 contains payload4194957,653bytes above4MiB but below8MiB. Maximum8MiB payload requires body8388624 and total8388628bytes. The root-approved runner codec matches existing Cooking body allowance FrameBytes+64 rather than modifying generic defaults or making internal production codec public.

## Scoped source

- RichFrameCodec.cs: explicit IFrameCodec/decoder using public NetworkFrameReader bounded to unchanged default Cooking FrameBytes+64; encoding delegates existing LengthPrefixedFrameCodec.
- RichPeer.cs: explicit FrameCodec injection (MaxFrameLength remains consistent but is not asserted to configure the generic decoder).
- RichFrameControls.cs and Program.cs: standalone `frame-controls`, bounded actual codec/wire controls with success/failure JSON. Exercises original red body and exact configured body through split prefix/header/payload; rejects configured body+1 on prefix, verifies default4MiB rejection and exact8MiB valid whitespace-padded Join envelope versus8MiB+1 rejection. Synthetic payload at framing allowance is deliberately codec-only and is not accepted Cooking business content.

SDK default Compile inclusion covers both new .cs files. Runner references runtime/transport through existing projects; no csproj/asmdef/shared source changed. Source-only `git diff --check` passed. Compiler and controls remain pending; no runtime pass claim.

Planned commands, only after root's sole .NET grant:

```powershell
dotnet build src/AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance/AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance.csproj --no-incremental
dotnet src/AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance.dll frame-controls
```

Actual ConnectionManager delivery and complete rich paired rerun remain required beyond codec controls. Existing red artifacts must be preserved.

## Unresolved transport upper boundary

LiteNetServerChannel.Receive enqueues every message before draining, against its separate default8MiB aggregate receive buffer (not merely inactive subscriptions). Therefore a maximum8MiB Cooking wire payload with20bytes framing exceeds that inbound message buffer. This is a distinct theoretical exact-upper-bound mismatch, not the observed local peer4MiB decoder failure; no buffer, wire or production change is approved in this increment. Root must separately review whether a transport configuration seam or tighter application envelope contract is appropriate. No physical LAN, performance, rich recovery or whole-network acceptance follows from these source changes.
