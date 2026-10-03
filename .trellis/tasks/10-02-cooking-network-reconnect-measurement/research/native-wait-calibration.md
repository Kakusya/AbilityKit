# Actual native wait primitive calibration and next diagnostic proposal

2026-10-03. Root calibration81994 actually terminated0; separate forced net10 Release build native0/zero warnings/errors, child run native0, no UDP/production edits. Local retained source, build/run stdout/stderr/native receipts, binary SHA/MVID and all600 raw timing samples: local/Artifacts/native-wait-calibration-20261003. Scope WAIT_PRIMITIVE_CALIBRATION_NOT_NATIVE_UPDATE_TRACE. Baseline changes no settings.

Actual runtime/OS/PID: {"runtime": ".NET 10.0.8", "os": "Microsoft Windows 10.0.26100", "pid": 40552, "elapsedMs": 10228.2432, "assemblySha256": "7C237CDEE44AF57A78FB6EF709B80A046E7DB653E1A04CB924DF3CF50F9299EB", "mvid": "50e41b6f-c35b-4b3c-97f8-6738f016d96d"}

Each group has20 warmups and200 measured samples; WaitOne executes on a dedicated ordinary-priority thread with an unsignaled AutoResetEvent (every actual returned signaled=false). Kernel GetThreadTimes records actual dedicated thread CPU in100ns units. Async Task.Delay records managed continuation IDs; no thread-CPU attribution across continuations. Empty bracket overhead is retained in O03 profile separately; these values are not silently subtracted. CPU clock granularity limits interpretation of zero reported dedicated CPU.

[
  {
    "kind": "unsignaled-AutoResetEvent.WaitOne",
    "requestedMilliseconds": 1,
    "count": 200,
    "medianMs": 15.49445,
    "p95Ms": 15.5558,
    "minMs": 15.0583,
    "maxMs": 16.0112,
    "threadCpuMs": 0.0
  },
  {
    "kind": "unsignaled-AutoResetEvent.WaitOne",
    "requestedMilliseconds": 15,
    "count": 200,
    "medianMs": 15.4916,
    "p95Ms": 15.6333,
    "minMs": 15.0846,
    "maxMs": 30.4711,
    "threadCpuMs": 15.625
  },
  {
    "kind": "Task.Delay",
    "requestedMilliseconds": 10,
    "count": 200,
    "medianMs": 15.4994,
    "p95Ms": 15.5965,
    "minMs": 15.0493,
    "maxMs": 16.0166,
    "threadCpuMs": 0.0
  }
]

Pinned LiteNet2.1.4 commit4d3de1e93abaead30199bf572f4a3363f854e14b uses UpdateLogic with remaining UpdateTime followed by AutoResetEvent.WaitOne(sleepTime), and its comments explicitly caution Windows low values. [Exact source](https://raw.githubusercontent.com/RevenantX/LiteNetLib/4d3de1e93abaead30199bf572f4a3363f854e14b/LiteNetLib/LiteNetManager.cs). Actual above primitive results support a scheduling-floor hypothesis, not the actual native loop wake frequency or sole cause of O03 full graph send-to-ACK latency. Source inspection finds both O03 managers assign requested15/1 beforeStart; no missed assignment established. Actual Profile81315 does not have per-row measured manager wake traces; requested values are not that proof.

## Proposed next diagnostic, not production approval

Investigate an explicitly reported process-scoped timer resolution lease in isolated diagnostics only. Microsoft documents that timeBeginPeriod no longer sets global timer resolution from Windows10version2004, needs matching same-period timeEndPeriod, and Windows11 occlusion can defeat the requested accuracy. [timeBeginPeriod](https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod), [timeEndPeriod](https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timeendperiod). Higher resolution can increase scheduler/power cost; it does not improve Stopwatch precision. These API facts alone do not establish an improvement on this machine.

Before source/runtime approval: independently review the proposal. Refuse non-Windows or pre-build19041 for this opt-in experiment; record actual OS, API return codes, requested timer state, actual wait measurements and matching finally disposal. Default request remains OFF; no power-plan/priority/kernel/global timer/socket changes. Preserve actual partial failures, no silent fallback or native defaults adopted. Whole bounded process exits release their own lease; error paths attempt exact same-period cleanup and record failures. Record actual reached timer effect, not API return as effective cadence.

First a separate tiny calibrated OFF/ON/OFF wait experiment under the same net10 runtime, no UDP, each unchanged20+200 samples and bounded wholeprocess; verify restoration and API returns. Only if independently accepted, consider isolated matched128-command mid graph two-by-two native15/1 and leaseOFF/ON, three fresh repeats each, with full typed graph/ACK/Ready/currentclose and the existing single owner/bounds/histories/component/resource/60overhead controls. Native scheduling measurements and Host application10ms waiting are distinct; precision can affect both, so the two factors cannot be conflated. No ordinary performance/rich4/physical claim from this diagnostic.

This proposal does not authorize source edits or executions by itself. Root retains the full N02/N03 exits in current-network-exit-refresh.md. Generic/main source remains unchanged; Unity and physical LAN remain deferred/unverified.


## Independent source review and narrow implementation grant

Root accepts the reversible diagnostic design after independent review. Before any build/run, scoped implementer owns ONLY local/Artifacts/native-process-timer-calibration-20261003/Program.cs corrections: exact argument validation before waits/API, intent vs actual Begin/acquired/released facts, immediate protected finally following successful Begin, bounded partial sample retention and both phase/cleanup error preservation. DefaultOFF; exactOFF/ON/OFF sample counts and whole45s budget for9groups, child50s; minimum supportedOS remainsWindows10build19041. Review freeze again before actualsourcecompile/runtime. This is an N03 local diagnostic increment, not generic/default/production adoption or global settings modification. No UDP/currentbusiness timeout/history changes. Original primitivecalibration source/logs/results remain immutable; summary-reviewed.json fixes derived async threadCPU placeholder to null/unmeasured, originalsummary retained.
