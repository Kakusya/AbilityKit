# 验证记录：正式配方与订单内容（2026-09-21）

> 任务 `09-21-cooking-formal-content-and-orders` 的验收方法、门禁实际输出、独立复验结论、变异测试结果与未覆盖边界。
> 本文件只记录实际执行过的命令与结果；未执行的项目明确标注。

## 1. 验收方法

- **证据分层**：`artifacts/cooking-formal-content/`（gitignored，本地指针）保存三类 JSONL evidence（loop/、configuration/、fingerprint/）与两份门禁的 `gate-summary.json` + trx；本任务的 `check.jsonl` 记录真实命令；本文件是方法、输出与结论的唯一汇总。
- **evidence 生成方式**：带 `COOKING_RECIPE_EVIDENCE_DIRECTORY` / `COOKING_CONFIGURATION_EVIDENCE_DIRECTORY` / `COOKING_FINGERPRINT_EVIDENCE_DIRECTORY` 重跑 Cooking 与 ET 测试套件（门禁通过后一次独立运行，46 个 JSONL）；每条记录含 before/after 状态哈希、操作、结果与 reason。
- **独立复验**：由独立子智能体复验 K01–K10（不运行 dotnet、不调用被测 C# 代码），结论见 §4。
- **变异测试**：对产品代码做四类定向变异，确认测试套件能杀死每一类，见 §5。

## 2. 门禁实际输出（真实命令）

### 2.1 cooking-kitchen-loop（P1，本任务验收 gate，未新增 gate）

```
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop
```

- Cooking domain build：成功（0 警告 0 错误）；
- Cooking ET runtime build with relation analyzer：成功；
- Kitchen-loop focused contract tests（`Gate=CookingKitchenLoop`）：**54/54 通过**（含新增 C01–C06、O01–O06、L01–L05）；
- Cooking domain and Level lifecycle regression：**175/175 通过**；
- Cooking ET Level host and runtime regression：**40/40 通过**（含 pickup/start-process/put-in 三个二进制指纹金样字节不变）；
- 结果：`Gate 'cooking-kitchen-loop' passed`；summary 与 5 个 trx 存于 `artifacts/cooking-formal-content/gate-kitchen-loop/`。

### 2.2 cooking-et-level-runtime（P1，回归）

```
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
```

- Cooking domain and Level lifecycle tests：175/175；Cooking ET Level host and runtime tests：40/40；
- 结果：`Gate 'cooking-et-level-runtime' passed`；证据存于 `artifacts/cooking-formal-content/gate-et-level-runtime/`。

### 2.3 未执行

- Unity compile / EditMode：未执行（Unity 环境不参与，任务范围禁止 Unity）。
- 协议检查（`compile-protocol-catalogs.ps1` / `export-protocol-wire.ps1`）：未执行（本任务不改协议目录与 wire schema；ET 命令指纹金样经既有测试证明字节不变）。

## 3. K01–K10 覆盖方法

| K | 方法 | 证据 |
|---|---|---|
| K01 内容加载 | C01（加载/身份/计数/dough 退役）、C02（乱序声明身份一致）、C05/C06（内容数值与容器/工位声明） | focused trx；`configuration/*.jsonl` |
| K01 坏内容 | C03 六种坏内容（订单模板缺 recipe、容器不接受产物、供应缺物品、cleanPool 非容器、数量为 0、station 缺失）→ 结构化诊断 | C03 测试；C02 旧注册表 evidence 显示 before==afterIdentity 零变更 |
| K02 订单簿 | O01（模板/身份校验、Open 可观察）、O02（Completed 一次）、O06（canonical 含订单簿与结算） | loop evidence |
| K03 提交契约 | O03 五个拒绝分支各断言结构化 reason；前三个分支 before/after canonical 相等，后两个分支（OrderAlreadyCompleted、ProductAlreadyConsumed）亦补 canonical 相等断言 | O03 |
| K04 幂等 | O04 同 identity `IsDuplicate` 无事件无二次结算；跨 identity 拒绝 | O03/O04 |
| K05 结算记录 | O02 全字段断言；O05 两单 Sequence=[1,2] 顺序=提交顺序；记录无评分字段 | O02/O05 |
| K06 闭环 | L01 全链路（开单→提交→碗脏→清洗回池，clean count 2→1→2）；L02 重放 canonical+hash 一致；L03 错误产物拒绝零变更；L04 上限 | `loop/L01` 16 条、`loop/L03` 7 条 JSONL |
| K07 烤面包 | 内容 bake-bread = bread-slice→toasted-bread；C01 断言；L03 走完整烘烤路径 | L03 evidence 末条 `OrderRequirementMismatch` + before==after hash |
| K08 wire 形状 | git diff 确认三个金样期望值未动；命令枚举与 `IsWellFormed` 不在 diff；S08/S10 尾插断言更新 | `fingerprint/{pickup,start,put-in}` evidence 与金样逐字一致 |
| K09 门禁 | §2 两份 gate 真实输出 | gate-summary.json + trx |
| K10 文档 | spec 头部修约 + index 两行 + progress/Todo 更新（见 §6） | git diff |

## 4. 独立复验结论

独立子智能体（不运行 dotnet、不调用被测 C# 代码）复验 K01–K10 与范围纪律：

- K01–K08 通过；K09 部分通过（门禁证据真实，但复验时 `check.jsonl` 与本验证记录尚未补齐——本文件与该记录即补齐动作）；K10 复验时为未完成（spec/progress/Todo 一处未改），本文件之后已完成。
- 复验发现并已处理：
  1. 三个被跟踪二进制 DLL（analyzer 插件、moba codegen、ET source generator）在任务会话内被构建改写——判定为构建副产物，已 `git checkout` 还原，不纳入本任务改动；
  2. 两处 v2 校验放宽（工位零能力、免工位配方豁免 CapabilityUnavailable）代码做了但文档未记——已补入 spec 修约表、index P3 行与 task design §2；
  3. O03 两个拒绝分支缺 canonical 相等断言、C01 未断言供应项数——已补；
  4. `CookingKitchenLoopFixtureTests` 文件头仍归属旧任务——已更正；
  5. design §1.1 示例能力清单漏 `beat`——已更正。
- 复验确认的负面事实（无越界）：diff 无评分/收益/评价字段、无失败条件、无前厅顾客/NPC 过程、无 UDP/KCP/检查点/Unity 源码改动；结算记录无评分字段；`artifacts/` 与 `local/` 未被 git 跟踪。

## 5. 变异测试（四类，全部被测试杀死）

| # | 变异（临时改产品代码后跑 focused 测试，随即原样回滚） | 结果 |
|---|---|---|
| M1 | 删除 `SubmitOrder` 的订单状态检查（Completed 订单可再次提交） | O03 失败（`OrderAlreadyCompleted` 断言）→ 测试有效 |
| M2 | 删除 `SubmitOrder` 的容器定义要求检查 | O03 失败（容器不匹配分支）→ 测试有效 |
| M3 | 删除 `ValidateRecipes` 对免工位配方的 `CapabilityUnavailable` 豁免 | C01/C02/C05/C06 失败（内容加载被拒）→ 测试有效 |
| M4 | canonical 快照去掉订单簿段 | 初版测试全过（存活！）→ 补 O06（canonical 含 orders/settlements）后重跑：O06 失败 → 测试有效 |

M4 的初版存活说明"canonical 可观察订单簿"原先只有 run-to-run 比较、没有内容断言；O06 即为此补齐。

## 6. 文档同步（K10）

- `.trellis/spec/cooking/cooking-recipe-loop.md`：头部新增"2026-09-21 正式内容与 order owner 修约"（四行映射表 + 实现状态声明 + 推定项清单）；2026-09-16 收口节与"当前边界/迁移边界"中"正式内容、订单/结算仍未启动"等表述改为指向两则 2026-09-21 修约。
- `.trellis/spec/cooking/index.md`：头部追加三次修约说明；P2 行追加正式内容与 order owner 已实现并验证；P3 行追加订单模板/供应校验面与两处规则放宽。
- `Docs/design/CookingGame/progress.md`：新增 2026-09-21 单机正式内容增量小节（§5），successor 段与"未完成范围"同步。
- `Docs/Todo.md`：Cooking ET 主线状态表述更新，正式内容/order owner 从未启动改为已交付，评分/失败/前厅仍未启动。

## 7. 未覆盖边界（明确不宣称）

- 评分、收益、评价与小关结算展示（owner 推迟；结算记录只含确定事实）。
- 失败条件与失败重试（owner 推迟）。
- 前厅顾客/NPC 过程、订单生成节奏、用餐离席（订单只能由前厅经 `OpenOrder` 注入，本任务不模拟生成节奏）。
- 过度加工与烧焦产物；跨小关装修/道具/Buff；检查点。
- Level/Map schema 对内容的正式引用、config identity 的 host/client 绑定（P3 successor）。
- 生产传输（KCP）、真实两 PC LAN、ET Phase B 权威迁移、ID string→long、Unity 一切范围。
- 固定伙伴最终人数未定，本任务不引入任何伙伴依赖。
