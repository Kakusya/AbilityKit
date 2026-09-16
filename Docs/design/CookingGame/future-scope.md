# Cooking Unity future scope

> 状态：**prohibited / non-active**。本文只保存历史来源、跨宿主不变量和将来重新审议时的条件性验收意图；它不是 active task、backlog、路线承诺、时间表或实施许可。

## Owner 决定

- 自 2026-09-16 起，在可预见的长期内禁止实施 Cooking Unity 范围。
- 禁止创建、推进或领取 Cooking Unity package、asmdef、scene、`MonoBehaviour`、authoring/export、projection、UI、动画、EditMode、scene smoke，以及为 Cooking 增加的 Unity 宿主接入。
- 本范围不再是七个 `09-15-cooking-*` 受限交付的完成条件，不是 non-Unity successor 的前置或 blocker。
- 路线、旧 checklist、spec 场景或本文本身都不能解除禁令。

## 重新授权条件

只有 owner 新作明确决定后，才可重新审议 Unity 工作。重新授权时必须：

1. 新建 Trellis task，不恢复七个已完成受限范围的旧 task。
2. 重新读取当时的纯 .NET 契约、产品范围、Unity 版本、package/asmdef 边界和测试门禁。
3. 明确批准具体宿主、场景、authoring、projection、UI 与验证范围；未被点名的 Unity 工作仍禁止。
4. 重新执行 Unity compile、EditMode/scene smoke 等适用检查；本文保留的场景不是已通过证据。

## 必须保留的跨宿主不变量

以下规则不依赖 Unity 是否存在，仍由纯 .NET 行为契约约束：

- 权威状态只由纯 C# 模拟、稳定排序、完整验证与原子提交改变；表现、动画、渲染帧和网络接收线程不得取得写权限。
- `DefinitionId` 与运行时 `InstanceId` 分离；Unity `GameObject` identity 不得成为 Session、Player、Match、Item 或配置的权威身份。
- 每个有效物品只有一个权威位置/所有者；拒绝、重复、过期、未知或跨 scope 输入不得产生部分 mutation。
- projection/authoring 若将来获准，只能导出或消费可序列化逻辑数据；旧版本、乱序、未知 identity、缺失映射和 stale input 不得覆盖较新视图，也不得回写 authority。
- session epoch、snapshot sequence、baseline reference、config identity 与 Match identity 必须继续隔离旧局、旧配置和旧快照。
- 表现优化不得改变命令结果、Match settlement、长期 progress 或持久化账本。

## 原 Unity 条目来源映射

| 阶段 | 原来源与条目 | 本文保留的条件性意图 |
|---|---|---|
| P0 | `09-15-cooking-interaction-foundation` T09-T10；projection、最小 scene/config fixture、EditMode | 只读 projection；stale/unknown 输入不覆盖新视图、不改变 authority；fixture 不定义产品人数或容量 |
| P1 | `09-15-cooking-lan-session` 的 Unity projection/scene/asmdef、连接 UI/控件与可视化发现 | UI 或可视化不得成为身份或 authority 来源；地址格式、发现协议、连接策略属于 non-Unity successor backlog，不在本文范围 |
| P2 | `09-15-cooking-recipe-loop` 的 scene/projection/UI/EditMode | 动画和 UI 不推进逻辑 Tick，不决定加工完成、装盘、交单或结算；order reject 不回滚已装盘状态 |
| P3 | `09-15-cooking-config-validation` 的 Unity 加载/EditMode smoke | authoring 输入须先形成候选配置并完成全量校验；失败不得部分替换有效配置 |
| P4 | `09-15-cooking-match-lifecycle` 的 authoring→layout export、projection、scene smoke | 导出稳定逻辑 layout；场景对象不推进 Match；旧/乱序 lifecycle snapshot 不覆盖新局 |
| P5 | `09-15-cooking-persistence-management` 的 Unity-host restart smoke | Unity 宿主只安装经 owner、版本、config identity 与完整性验证的长期状态，不隐式恢复 Match 或重复奖励；non-Unity app-host/process restart 属于 successor backlog |
| P6 | `09-15-cooking-network-measurement` 的 Unity projection、插值/预测/纠正表现验证 | 优化默认关闭并受 evidence/owner gate 控制；表现变化不得污染 authority、settlement 或 progress |

## 证据边界

上述 Unity 条目均为 **not-run / prohibited**，不得记录为 pass。七个旧 task 的完成仅指各自 `check.jsonl` 已支持的纯 .NET 受限增量；完整 P0–P6 产品出口、Unity 可玩版本和跨宿主验收均未完成。
