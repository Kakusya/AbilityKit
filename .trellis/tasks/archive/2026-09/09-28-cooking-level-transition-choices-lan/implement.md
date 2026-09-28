# Cooking 跨关选择与状态承接 LAN 纵切：实施清单

需求见 [prd.md](prd.md)，设计见 [design.md](design.md)。未批准前不修改产品代码、不运行 `task.py start`。

## Ordered implementation

1. 建立 session-level canonical projection、scope/generation/sequence 与稳定 SHA-256。
2. 扩展 LAN protocol，使 recipe command、transition result 和完整 session snapshot 携带当前 Level identity。
3. 在 Host 增加 Host-authoritative transition coordinator：选择校验、成功 handoff、major checkpoint write、Level generation install 和广播的原子顺序。
4. 在 Client 增加新 Level baseline 接收、旧水位拒绝、pending command 清理和重连后的完整投影恢复。
5. 使用已有领域能力接入装修、烤箱解锁、煮制加速和 checkpoint 失败注入；禁止复制领域迁移规则。
6. 增加纯协议/投影测试、Host 单元测试和 Loopback UDP 双端验收，覆盖旧命令/旧快照、重复 transition、断线重连与 SHA-256 共识。
7. 运行定向测试和既有 gate；把实际通过、失败、跳过和原因写入本任务 `check.jsonl`。
8. 执行 `trellis-check`，补充必要的 Cooking spec/progress/Todo 证据，再归档任务。

## Validation commands

```powershell
dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter "FullyQualifiedName~CookingSession|FullyQualifiedName~CookingMajorProgress|FullyQualifiedName~CookingLevel"
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
git diff --check
```

如果仓库实际项目名或 gate 入口不同，以 `tools/test-gates.json` 和 `.csproj` 为准，不把未执行命令记录成通过。

## Risk and rollback points

- protocol schema 扩展：先保留旧 decode 路径，避免破坏已有单 Level 测试。
- checkpoint 原子性：任何写入失败都必须在状态发布前返回，必要时使用现有 checkpoint restore seam 回滚。
- generation 水位：Client 不能仅依赖 sequence 判断新旧 Level，必须先比较 scope/generation。
- 失败注入：使用调用方提供的不可写/冲突目录或测试 double，不能破坏工作区文件。

## Exit checks

- [x] PRD、design、implement 与 JSONL manifests 完整。
- [x] 新增行为只在独立 task 下实现，规划源任务无业务代码改动。
- [x] Host/Client 完整 session SHA-256 共识、旧消息拒绝、重连恢复和 checkpoint 失败阻断均有测试证据。
- [x] `check.jsonl` 记录实际 pass 与检查代理 blocked 事实。
