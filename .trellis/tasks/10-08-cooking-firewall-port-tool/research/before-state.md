# Before-state and source review

2026-10-08 source aa5e26cc8b05d24f86e7952b7ffa495a03f535b1. Windows PowerShell 5.1, elevated token verified. Prior unrelated untracked private/cache paths retained. Git/Orca enumerate only master for AbilityKit; no issue6-test-gate-results checkout is available. Other Orca repositories are unrelated and untouched.

Owner observed firewall popup from real Cooking NetworkAcceptance apphost, UDP18090. Observation host PID65344 stopped after confirmation; no client or remote probe launched. Program is D:/MyWorkTree/AbilityKit/src/AbilityKit.Game.Cooking.NetworkAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkAcceptance.exe. WLAN6 has Public network category. Persistent local program rules contain enabled Public inbound TCP Block Any and UDP Block Any. Tool-owned rule is initially absent.

Raw before-program-rules.json, before-network-profile.json and before-environment.json are preserved under ignored local/Logs/cooking-firewall-tool/20261008-113245. The collection command ended native1 because its final lookup of the absent owned rule returned no match; this is retained, not a test pass. It successfully wrote the three evidence files before that lookup.

Native Windows NetSecurity cmdlets suffice; no dependency, vendoring, C# framework or Unity changes are necessary. Existing NetworkAcceptance reports READY {port} {PID}; current process acceptance wrapper checks PID before passing the actual port to Client. Host-launch/range allocation is a separate optional scope, not implicitly included in the current firewall request.

Reference contracts: Microsoft Windows Firewall rules (explicit Block precedence): https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/windows-firewall/rules ; New-NetFirewallRule (protocol/port/program/profile restrictions): https://learn.microsoft.com/en-us/powershell/module/netsecurity/new-netfirewallrule ; Test-NetConnection (TCP Port test, not UDP): https://learn.microsoft.com/powershell/module/nettcpip/test-netconnection .
