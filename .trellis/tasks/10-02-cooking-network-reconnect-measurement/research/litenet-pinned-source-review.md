# Pinned LiteNet source review - 2026-10-03

Root read the local transport csproj and current measurement deps.json: .NET transport references LiteNetLib2.1.4, actual measurement dependency names runtime assembly2.1.4.0. Shared transport sources are compiled from Unity/Packages; this does not verify the Unity plugin DLL or a Unity build. No.NET execution, library upgrade or transport edits occurred.

## Primary-source findings

[LiteNetManager at tag2.1.4](https://raw.githubusercontent.com/RevenantX/LiteNetLib/2.1.4/LiteNetLib/LiteNetManager.cs) initializes UpdateTime=15ms. Its update loop waits on the update trigger for remaining interval; documentation exposes TriggerUpdate for more frequent sending and cautions about Windows timing. This establishes source behavior, not the actual observed scheduling distribution of our binary under load.

[NetConstants at tag2.1.4](https://raw.githubusercontent.com/RevenantX/LiteNetLib/2.1.4/LiteNetLib/NetConstants.cs) defines a64packet reliable window,1MiB socket buffers and initial MTU1024. These are library constants, not application Session capacity or guarantees of effective operating-system buffer/MTU values.

[ReliableChannel at tag2.1.4](https://raw.githubusercontent.com/RevenantX/LiteNetLib/2.1.4/LiteNetLib/ReliableChannel.cs) initializes its window from that constant. SendNextPackets sends pending acknowledgements and admits queued packets only while the reliable window has room; ProcessAck releases window entries. This makes complete large-message fragmentation/window scheduling a concrete investigation candidate, but does not attribute the measured3s projection delay to this path.

## Local contract and next experiment

Local LiteNetTransport and LiteNetChannelListener create NetManager with UnsyncedEvents/AutoRecycle and no explicit UpdateTime. Both sends use ReliableOrdered. They do not call TriggerUpdate. A changed update schedule would affect the generic shared transport and both endpoints; preserve existing defaults unless an explicitly reviewed opt-in is adopted. Inspect public API and thread/close behavior before proposing an option; do not invoke authority from transport callbacks or alter reliable ordering.

A future source-frozen experiment must report effective library settings, actual package/binary provenance, message size/cohort and complete projection latency, compare identical workloads with original defaults, and run appropriate generic transport/SDK gates. Raw relay counters can characterize traffic, but payload growth, owner CPU and window scheduling require separate attribution. No compression, MTU/window constant patch, new transport, budget inflation or post-hoc target relaxation is approved by this review. Initial v2.1.4 URL lookup returned404; verified tag is2.1.4, and no findings rely on the failed URL.
