# Cooking 单机多订单菜单纵切 — Technical Design

## 1. Scope And Ownership

本任务扩展既有纯 C# Cooking 前厅和正式内容，不建立新的订单、评分或清洗状态机：

```text
cooking-content-v2.json
  ├─ plate definition + cleanPool supply
  ├─ toasted-bread-order (50 temporary points)
  └─ existing recipe/order content remains data-driven

Level / ET host input
  └─ ordered temporary menu [soup, toast]

CookingFrontOfHouse
  ├─ customer arrival sequence (existing source of truth)
  ├─ ordered menu bound for the current Level
  ├─ template = menu[(arrivalOrder - 1) % menu.Count]
  └─ customer snapshot/checkpoint stores selected template

CookingRecipeSimulation
  ├─ existing multi-template order book
  ├─ existing per-template score calculation
  └─ existing generic washable-definition/clean-pool mechanics

CookingLevelEtHost
  └─ owns one front-of-house menu reference and reuses domain checkpoint
```

LAN/session、Unity、长期存档和正式数值平衡不进入本任务。

## 2. Formal Content Changes

### 2.1 Plate

在 `cooking-content-v2.json` 增加：

- `plate` 物品定义；
- 容量 1；
- 只接受 `toasted-bread`；
- `standardInitialSupply` 增加 `plate x2`，位置为 `cleanPool`。

同时从 `bowl.acceptedDefinitions` 移除 `toasted-bread`，保证汤和面包的上菜容器严格区分。

现有 clean-pool 构造已经按 definition 维护数量，现有 fixture 已使用 `WashableContainerDefinitions`。盘子复用同一机制；不复制 plate-specific 清洗队列。现有 bowl 命名的测试辅助入口可保留兼容，除非它实际阻止多 definition 验收。

### 2.2 Toast Order

增加订单模板：

```text
id: toasted-bread-order
requiredRecipe: bake-bread
requiredContainerDefinition: plate
baseScore: 50
```

番茄蛋花汤保持 `BaseScore = 100`。星级阈值继续为临时 100/200/300。

### 2.3 Configuration Identity

当前 `CookingOrderTemplateDefinition.BaseScore` 已参与运行时评分，但配置 canonical 的 `CanonicalOrderTemplate` 尚未包含该字段。本任务必须把 `BaseScore` 加入配置 canonical/hash；否则两端或恢复路径可能在分值不同的情况下仍得到相同配置身份。

该修正适用于全部订单模板，不为烤面包单独计算哈希。

## 3. Temporary Menu Contract

新增不可变有序菜单值对象，例如：

```csharp
public sealed record CookingFrontOfHouseMenu(IReadOnlyList<OrderTemplateId> Templates)
```

约束：

- 非空；
- template identity 非空；
- 不允许重复；
- 顺序进入 canonical；
- 第一版内容为 `[tomato-egg-soup-order, toasted-bread-order]`。

该菜单是当前 Level/ET host 的显式输入，不新增 `cooking-content-v2.json` 顶层菜单字段。原因：正式 Level/Map schema 尚未建立，把临时轮换塞进全局内容文档会错误固化产品归属。菜单中的模板 ID 仍必须引用厨房 fixture 已加载的订单模板，并在实际开单/恢复校验中验证。

模板选择是纯函数：

```text
Templates[(ArrivalOrder - 1) % Templates.Count]
```

因此第一版序列为汤、面包、汤、面包。该奇偶轮换是临时验收规则，不增加随机种子、权重、偏好或独立菜单游标。长期订单生成另立任务。

## 4. Front-Of-House API And Compatibility

当前前厅方法每帧接收一个 `OrderTemplateId`。为避免修改 LAN/session 路径，本任务采用兼容扩展：

- 新增接收 `CookingFrontOfHouseMenu` 的 `Step`、`FinishInProgress`、`ResetForNextLevel` 和 `ExportCheckpoint` 重载；
- 既有单模板重载继续存在，并委托 `CookingFrontOfHouseMenu.Single(template)`；
- 菜单第一次使用时绑定到当前 `CookingFrontOfHouse`，同一 Level 后续传入不同菜单必须结构化拒绝或抛开发期参数错误，不能中途改变已生成序列；
- `CookingSessionHost` 等现有单模板调用无需修改。

ET host 增加 `UseFrontOfHouse(house, menu)` 重载，并保留现有单模板重载。ET 内部只保存菜单，不保存第二个模板选择游标。

## 5. Customer And Snapshot Contract

`CookingCustomer` / `CookingCustomerSnapshot` 增加可空 `OrderTemplateId`：

- `WaitingForInquiry` / `InquiryInProgress`：模板为空；
- 成功开单后：保存按 `ArrivalOrder` 选出的实际模板；
- `Ordered` / `Dining`：模板必须非空，并与厨房 order book 中同一订单的模板一致。

`CookingFrontOfHouseSnapshot` 增加有序菜单。canonical/SHA-256 覆盖：

- 菜单模板顺序；
- 每位已开单顾客的实际模板；
- 已有 `NextCustomerSequence`，它同时是下一次临时轮换位置的唯一水位。

不增加独立 `NextMenuIndex`。

## 6. Inquiry And Submission Flow

完成询问时：

1. 以顾客 `ArrivalOrder` 从绑定菜单选择模板；
2. 调用现有 `kitchen.OpenOrder(orderId, template)`；
3. 只有开单 accepted 后，把顾客阶段、订单 ID 和模板一起提交；
4. 开单失败保持顾客和菜单水位不变，伙伴任务不计数。

玩家制作流程：

- 汤：既有番茄、鸡蛋、锅、灶台、碗路径；
- 面包：面包片放入烤箱，启动 `bake-bread`，到点生成 `toasted-bread`，取出并放入干净盘，再提交对应订单。

现有订单提交校验已经比较 recipe 和 container definition；本任务增加正式内容与跨模板验收，不复制提交判断。

## 7. Washable Container Flow

汤提交后碗变脏，面包提交后盘子变脏。两者进入现有 `DirtyBowlsAwaitingWash()` / `CompleteWash(ItemId)` 实现所依赖的通用 washable-definition 集合。

实现要求：

- 清洗队列按 item identity 处理，不按 bowl/plate 分叉；
- 成功清洗后增加该物品自身 definition 的 clean-pool count；
- 盘子清洗不得增加碗池，反之亦然；
- 每次成功清洗仍计为一个伙伴任务；
- 不进行无关的大规模 API 重命名。

## 8. Checkpoint And Format Version

前厅 checkpoint 需要保存：

- 有序菜单；
- 每位顾客选中的模板；
- 既有顾客、伙伴、队列、计数和时钟状态。

恢复校验新增：

- 菜单非空、无空 identity、无重复；
- checkpoint 的 legacy `ActiveOrderTemplate`（若为兼容保留）必须等于菜单第一项；
- 已开单顾客模板等于 `menu[(ArrivalOrder - 1) % Count]`；
- 顾客模板与厨房订单模板一致；
- 未开单阶段不得携带模板。

该载荷改变 `CookingLevelCheckpoint` 的序列化形状。`CookingLevelCheckpointCodec` 格式版本从 v2 升至 v3；v1/v2 结构化拒绝，不提供迁移。原因是当前没有 durable/process-crash 产品兼容承诺，静默采用缺失字段比明确拒绝风险更高。

## 9. ET Recovery

`CookingLevelEtHost`：

- Running Tick 使用绑定菜单推进前厅；
- Export 复用前厅 checkpoint；
- Restore 从 checkpoint 前厅状态恢复菜单并重新绑定；
- 恢复后下一顾客继续由 `NextCustomerSequence` 派生相同模板；
- ET 不维护 `NextMenuIndex` 或第二份顾客模板表。

验收比较不中断与恢复两臂的：订单模板序列、前厅 canonical、厨房 canonical、settlement、总分、星级和 HostFrameSequence。

## 10. Compatibility And Scope Control

- 既有单模板前厅测试通过兼容重载保持行为不变。
- 现有 session/LAN API 和 wire 不修改；新菜单只在纯 C#/ET 单机验收使用。
- 配置 identity 因新增内容与 `BaseScore` canonical 修正必然变化；所有测试必须从同一内容文档加载，不手写旧 hash。
- `09-19` 中“烤面包只作半成品”的旧决定已被 2026-09-29 最新 owner 决定显式覆盖；本 task 文档必须保留该冲突说明，避免未来会话误把旧来源恢复为现行规则。
- 不加入第三菜品、随机菜单、顾客偏好、收益、复杂评分或长期解锁。
- 全部数值仅为临时基线，门禁通过不表示平衡定稿。
