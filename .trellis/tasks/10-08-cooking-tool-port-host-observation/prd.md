# Cooking tool-selected host port and timed observation

## Goal

Owner requires obtaining a concrete port through the installed tool, passing its result to the Cooking host, then closing that owned host five minutes after launch. User corrected the previous manual selection approach before any host was started. This explicitly authorizes the bounded GetPort extension and timed local observation.

## Requirements

- Add read-only `cook-firewall -Action GetPort`. Load existing shared configuration, query local UDP endpoints, select the first available port in that configured range, return JSON `port`, `portReserved=false`, config source/path. Report exhaustion and endpoint-query errors with Failed/nonzero; never choose outside the range or mutate firewall/PATH/config. This is a candidate selection, not a lease or proof of LAN connectivity; actual host binding/READY is authoritative.
- Extend isolated tests with mock Get-NetUDPEndpoint. Verify saved/explicit range consumption, IPv4/IPv6 occupancy, duplicates, exhaustion, query failure, and no mutation. Preserve existing27 controls.
- Update AGENTS to mandate using GetPort output before launch; do not duplicate range selection in caller. If binding conflicts, reacquire through the tool; other startup failures surface directly.
- Install updated tool to existing fixed bin. Main launches only Cooking NetworkAcceptance with its returned port; record tool result, real PID/start time/READY/socket ownership. No firewall changes or gameplay changes.
- Independent hidden watchdog owns the launched host handle and stops it at startTime+300 seconds, even if conversation is interrupted. It must never stop an unrelated PID/process. Keep launch/stop/stdout/stderr receipts under ignored local/Logs. Never claim popup observed or physical-LAN success from program startup.

## Acceptance Criteria

- [x] Focused isolated controls/parser and review passed; updated installed tool returns a concrete port.
- [x] Tool port, actual host READY and UDP ownership agree.
- [x] Host remains available for observation and exits at the five-minute deadline with stop receipt.

## Notes

- Only tools/cooking-firewall.ps1, tools/cooking-firewall.tests.ps1, AGENTS.md and this task's records change. Existing defaults and firewall policy remain unchanged. No host-runner product, daemon, leasing subsystem, Unity, example code, commits or other-tree cleanup. Initial source base aa5e26cc8 with prior firewall task dirty changes; preserve them. Native Trellis worker owns two tool files; main owns docs, install and timed observation. Previous before-dev/spec context applies; manifests provide child-side fallback.
