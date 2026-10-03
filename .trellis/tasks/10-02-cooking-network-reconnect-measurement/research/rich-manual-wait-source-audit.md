# Original rich manual600s wait source audit

2026-10-03. Read-only source/raw audit, no .NET or production changes. Original evidence local/Logs/cooking-network-rich-recovery/20261003-093858-7855642/manual-paused/{host,client}.json remains unchanged. This report distinguishes current main source mapping from original frozen compiled source b60f174fa; no reexecution or attribution claim.

## Exact outstanding step

This failure is BEFORE the Running manual cut, not a failure resuming a paused manual process. Client source RichRunner.Client executes initial grant -> Chef preparation marker -> CompleteAvailableManualHandoff -> ProduceAndPlate(D31) -> Go(partner-parking) -> Mark(partner-drink-prepared). Host is in StageWait for that last marker, then would prepare F01 components, park, transition Running and mark chef-running.

Original client operations: StartProcess329; TakeOut340(product-81,v0); PutIn349(product-81,v14); parking Move350 onward. Last sent is remote-process-action-366, Move(0,+1000), facing(0,1); received Accepted terminals only through365. The actual failed predicate is RichRunner.Send -> Wait(() => _peer.Has(correlation), terminal-remote-process-action-366). targetResultVersion remains null: the subsequent committed-caller-projection predicate was not reached for366. Parking and partner-drink-prepared marker were not completed. Host failure stage remote-prepared-D31 matches this dependency, with only chef-procurement-prepared phase recorded; client phases empty. F01 preparation, Running cut, deliveries, natural close and successor absent.

The terminal wait searches RichPeer.Results for the matching correlation. It is not an explicit wait for newest Ready. Once a terminal arrives Send checks Accepted, then waits latest baseline recipe version>=terminal StateVersion. Host StageWait tests a real accepted dedup receipt for DomainId(participant, stable partner-drink-prepared), not merely container content or position.

Original Host observedIngress contains ACK5431 followed by the actual remote-process-action-366 on channel1. Host observedReplies has NO366. Therefore the command was seen at Host framework observer ingress, but neither a Host terminal observation nor a Client terminal receipt is retained. This is narrower than claiming the last command was lost on UDP or proving it entered business admission/executed. Host latest recipe5555 vs Client5542 does not alone prove366 committed.

## ACK/state evidence and limits

Client latest validated identity5431, exact ACK sent5431, prior current-generation Ready5415; same server instance90368748df14432abc14e0ae98e76978/generation1/scope service epoch1. Ready receive ordinal1932; baseline receive1934; ACK send1935; command366 sent1936. The old grant is retained evidence, not fabricated newest Ready. RichPeer.Command currently does not wait CurrentBusinessGrant/newest Ready before every command; Poll drains incoming and ACKs newest validated image, then Command sends sequential wire. This source fact is not proof of a bug: Session must determine admission against actual current identity/state, and original report lacks that specific frame disposition.

Host latestHostIssued/Ack/Ready failure fields are null. Its observer has baseline identities, ACK ingress and grants, but final error does not provide complete correlated latest Session diagnostics/Waiting/DeferredAck, command366 admission/disposition/domain receipt, relevant owner-frame timing or callback queue timing. Original reencoded5,209,093/5,218,268B are size probes, not actually received bytes or latency causes.

RichRunner uses RichPeer.cs, NOT NetworkProcessMeasurement/FramedFaultPeer.cs. The latter belongs to separate process/impairment measurement and checks Ready in Queue with per-correlation completion; its state cannot explain this original rich caller. Treat cross-runner rules separately.

## Minimal prerequisite before a correction

Add reviewed bounded application diagnostics around existing waits, without new capture/ticks/commands, graph pruning, ACK manipulation, arbitrary Ready requirement or timeout/cap enlargement:

- Record exact in-flight wire/domain identity, Host ingress ordinal/time, owner-frame admission/disposition/terminal enqueue and retained dedup lookup for366; snapshot existing latest Session diagnostics/participant Issued/Ack/Ready/DeferredAck when available.
- Record actual callback full-frame byte count and copy/decode/queue/Poll timestamps, matching callback terminal/ACK/Ready identities, queue counts and errors, latest projection version and predicate result. Keep send enqueue vs native delivery distinction.
- Record planner logical substep/destination/path segment and actual poses already present in the current capture so parking progress and missing marker are unambiguous; do not replace moves or reset routes.
- Use bounded ring/summary or schema-bounded records preserving complete authoritative history; disclose dropped diagnostic records explicitly, never drop business receipts. Freeze source/build and retain original red before a genuine same-scope run.

Only if correlated frames demonstrate a specific pending-ACK/admission or caller-loop ordering defect should a narrowly reviewed ordering correction follow. Timer/native profile findings can justify another experiment, not retrospectively explain this failure. Full four-cut rich acceptance, strict verifier checks, ordinary performance and physical LAN remain separate incomplete exits.
