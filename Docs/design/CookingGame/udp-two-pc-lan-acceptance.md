# Cooking UDP 两台物理 PC LAN 验收

> 状态：手动验收出口。本文不是已完成证明；没有两台物理 PC 的同次 artifact 前，不得将 loopback 或同机跨进程结果写为两 PC LAN 通过。

## 前提

1. 两台电脑运行同一 commit/build，使用同一 LiteNet connection key、Cooking protocol 和 config identity。
2. 房主电脑选择实际 LAN 网卡与 IPv4 地址；客户端能到达该地址和 UDP 端口。
3. 两端分别记录网卡名称/状态、IP、端口和防火墙允许/阻止状态。脚本或 harness 不修改防火墙。
4. 房主电脑运行 host，同时以 `udp-player-one` 参与；客户端以 `udp-player-two` 加入。

## Host 命令

```powershell
dotnet run --no-build --project src/AbilityKit.Game.Cooking.UdpHarness -- `
  --role host --port 28080 --topology udp-two-pc-lan `
  --nic-identity "Ethernet / 192.168.1.10" --nic-status "connected" `
  --firewall-status "UDP 28080 allowed" --artifact-directory artifacts/cooking-udp/two-pc-host `
  --duration-seconds 45
```

保存 `host-ready.json`、`host-result.json`、stdout/stderr。`host-ready.json` 的端口、protocol 和 config identity 必须与客户端命令一致。

## Client 命令

```powershell
dotnet run --no-build --project src/AbilityKit.Game.Cooking.UdpHarness -- `
  --role client --peer 192.168.1.10 --port 28080 --topology udp-two-pc-lan `
  --nic-identity "Wi-Fi / 192.168.1.11" --nic-status "connected" `
  --firewall-status "outbound UDP allowed" --artifact-directory artifacts/cooking-udp/two-pc-client `
  --timeout-seconds 20
```

保存 `client-result.json`、stdout/stderr。该最小场景验证 client handshake、authority-bound player、baseline、remote command、delta 和 client state hash。

## 必须核对的证据

- 双方 artifact 的 `topology` 均为 `udp-two-pc-lan`，且环境元数据、endpoint、NIC 和 firewall 状态完整。
- `protocol` 与 `configIdentity` 完全一致。
- host/client 的最终 `stateHash` 一致。
- host diagnostics 含 remote `handshake-accepted`、`baseline-installed`、`command-enqueued` 和 `command-executed`；断开后应有 `transport-loss` / `resource-unbound`。
- client result 的 synchronization 为 accepted，sequence/baseline reference 连续。
- 附带双方 git/build identity、日期和明确结论。失败时保留原始 artifact，不能回填为通过。

## 当前边界

这不是自动重连、endpoint rebinding、主机迁移、WAN/NAT、中继、认证、性能阈值、预测或 rollback 验收。性能阈值仍为 `UNSET`；两 PC 成功只证明此最小 UDP listen-host 场景，不自动开启优化。
