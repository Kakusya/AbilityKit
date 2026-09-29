# Cooking 单机多订单菜单纵切 — Implementation Plan

## Preconditions

- 当前任务已于 2026-09-29 获 owner 明确批准并进入 `in_progress`；实现仍限于本文件定义的纯 C#/ET 范围。
- 实施前运行 `task.py start cooking-singleplayer-multi-order-menu` 并加载 `trellis-before-dev`。
- 保留 `09-19-cooking-productization-network-slice-planning` 为 planning-only 产品事实来源，不在该 task 下签入业务代码。

## Ordered Checklist

1. **先补正式内容与配置身份测试**
   - 断言 plate、toasted-bread-order、临时 BaseScore 和 cleanPool supply。
   - 断言 bowl 拒绝 toasted-bread、plate 只接受 toasted-bread。
   - 断言 `BaseScore` 进入配置 canonical/hash。
2. **补菜单值对象与前厅失败测试**
   - 验证菜单非空、唯一、有序 canonical。
   - 验证汤/面包固定轮换和单模板兼容重载。
   - 验证开单失败不推进顾客状态或伙伴成长。
3. **实现客户模板投影**
   - 顾客开单成功后保存实际模板。
   - snapshot/canonical 增加菜单和顾客模板。
   - checkpoint 校验模板阶段、轮换结果和厨房 order book 一致性。
4. **接入正式盘子与面包订单**
   - 更新 `cooking-content-v2.json`。
   - 复用现有 recipe、PutIn、SubmitOrder 和评分路径完成面包闭环。
   - 验证错菜、错容器、重复提交 mutation-free。
5. **验证盘子清洗与伙伴成长**
   - 面包提交后盘子进入现有可洗队列。
   - 清洗后恢复 plate clean-pool，不污染 bowl count。
   - 伙伴成功清洗盘子增加一次任务计数。
6. **升级 checkpoint v3**
   - 前厅 checkpoint 保存菜单和顾客模板。
   - codec 当前格式升 v3，v1/v2 明确拒绝。
   - 增加菜单/模板/厨房关联投毒的原子拒绝测试。
7. **扩展 ET 单机恢复验收**
   - 增加不中断臂和恢复臂，至少生成并完成一份汤、一份面包。
   - 比较后续模板序列、canonical/hash、settlement、总分、星级和水位。
   - 保持 session/LAN 项目零修改。
8. **同步规范与证据**
   - 门禁通过后更新 Cooking spec、progress 和 Todo。
   - 明确固定轮换及全部数值为临时方案。
   - 把实际命令结果追加到 `check.jsonl`。

## Validation Commands

```powershell
dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter "FullyQualifiedName~CookingContentCatalogTests|FullyQualifiedName~CookingFrontOfHouseTests|FullyQualifiedName~CookingOrderBookTests" -v minimal

dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter "FullyQualifiedName~CookingLevelClosedLoopTests|FullyQualifiedName~CookingLevelCheckpointTests" -v minimal

powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop

powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime

git diff --check
```

Unity、LAN、KCP、durable/process-crash 验证不在本任务范围，不得记录为通过。

## Risky Files And Rollback Points

- `src/AbilityKit.Game.Cooking/Content/cooking-content-v2.json`
  - 内容 hash 必然变化；订单模板和供应必须作为一个原子内容变更验证。
- `src/AbilityKit.Game.Cooking/CookingConfigurationValidation.cs`
  - `BaseScore` 加入 canonical 会改变所有包含订单模板的配置 identity；不得遗漏相关 hash 测试。
- `src/AbilityKit.Game.Cooking/CookingFrontOfHouse.cs`
  - 风险集中在单模板兼容、多模板轮换、顾客模板阶段约束和 checkpoint 原子恢复。
- `src/AbilityKit.Game.Cooking/CookingLevelCheckpoint.cs`
  - 格式版本升级必须同步 codec 和旧版本拒绝测试。
- `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs`
  - 只保存菜单并复用领域状态；不得复制模板游标。

## Definition Of Done

- 正式内容有汤和烤面包两个可提交订单，盘子与碗语义分离。
- 临时轮换序列可确定重放，单模板兼容路径不回归。
- 两种订单均可制作、提交、结算和计分；错误路径零变更。
- 碗和盘分别进入正确 clean-pool，伙伴清洗共用同一任务流。
- snapshot/canonical/checkpoint/ET restore 覆盖菜单和顾客模板。
- checkpoint v3 明确拒绝旧版本，无静默缺字段恢复。
- Cooking 与 ET runtime 门禁通过，无 session/LAN/Unity 改动。
- 文档明确固定轮换和全部数值为临时方案。
