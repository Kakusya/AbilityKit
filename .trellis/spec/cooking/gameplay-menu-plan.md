> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# Cooking 功能与菜单推进计划

日期：2026-10-02。当前owner明确要求全部审议后实施到完成；架构保持，先单机，再网络，再单机Unity、网络Unity。审议结论见总任务research/final-review.md，实际实施与停止/恢复回执见[执行记录](../../tasks/10-02-cooking-gameplay-menu-plan/execution.md)。Unity独立执行门未解除。

本次审议：S01–S14/N01–N03的设计与范围已细化，审阅发现的保留分支缺口须在合入前修正。核心与菜单恢复实施，经营整合按依赖推进；详见[S01设计](../../tasks/10-02-cooking-singleplayer-spatial-interaction/design.md)及各子Task。

## 目标与范围

建立操作清楚、物品可交接、制作与交付分离的做菜经营玩法。创新“食客也是食材”、复杂操作小游戏不进入基础范围。长期支持合作，但当前专注单机纯 C#；多个逻辑玩家争抢的领域测试不代表联机或多人可玩。

Owner 明确顺序：**单机 → 网络 → 单机 Unity → 网络 Unity**。它是推进顺序，不是完成状态或实施许可。网络未启动；两类 Unity 仅登记禁止执行的条件性 Task，既有长期禁令保持。每个实施 Task 开始前仍须审阅 PRD/design/implement/manifests 并得到批准。

## 阅读入口与唯一归属

- [当前进度](../../../Docs/design/CookingGame/progress.md)：已验证能力及历史证据入口。
- [逐项功能对照](../../tasks/10-02-cooking-gameplay-menu-plan/research/feature-audit.md)：A01–K14、额外机制和餐厅建造；含状态、代码和历史 check 指针。
- [Task 注册表](../../tasks/10-02-cooking-gameplay-menu-plan/task-register.md)：顺序、依赖、范围、验收、真实 planning Task 链接。
- [菜单导入规划](../../../Docs/design/CookingGame/reference/menu-integration.md)：87 款候选与现有 schema 的差异、分批方案、默认规则。
- [原始菜单资料](../../../Docs/design/CookingGame/reference/menu-v0.1/README.md)：Markdown/Excel 原始副本及摘要，不作 runtime 配置。
- [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md)：保留现有架构、未来能力所在层、冲突登记。
- [.trellis 规划任务](../../tasks/10-02-cooking-gameplay-menu-plan/prd.md)：本轮授权与记录验收。

以上 plan 与 reference 不替代生效行为契约或实施 check。生产代码不按下载文件直接生成，不把全目录写入首关，不以“配置可表达”推断“87 款已实现”。

## 本轮规划决定（尚未实施）

下列细节由 owner 授权助手补足；它们是后续 Task 的设计输入，涉及既有行为时必须显式修约。

1. 普通手持槽和普通台面各容纳一个物件。特殊库存/设备槽独立配置；容器容量不能绕过空间占位。
2. 物品唯一位置、包含无环、原子提交、拒绝零变更、去重和稳定仲裁保持。物品 Definition 与实例分离。
3. 自由连续移动与离散放置槽分离。当前仅规划纯 C# 位置、朝向、距离、障碍与目标解析；未来 Unity 只消费结果。交互候选按距离、朝向、稳定 ID 决胜；不同动作先预判合法性，再提示动作，执行时再次校验。
4. 基础交互入口统一：空手拿取；持物面向兼容容器优先添加；面向空槽放置；可盛装时转移成品。拒绝保留物品并输出原因。重复按键不重复转移。投掷、接取和冲刺后置。
5. 手工作业松手/离开暂停并保留进度；接手不重置，不因同时两人操作自动加速。设备作业启动后继续；端走容器沿用已生效“加工继续、内容锁定”，不得偷偷改成移开就停。实现手工作业前修约现有加工模型。
6. 成品/半成品可暂存；容器带走内容。批量内容显式记录剩余份数和物料守恒，按份取用不得混用现有“整份 Pour”。配方前置拓扑支持独立分支，不强制全链顺序。
7. 成品身份与订单身份分离。饮品独立贴单；餐食不强制贴标，提交时核单。交付前可显式解除/换绑，交付后禁止；绑定不改变配方内容。多人未来不得重复绑定或重复提交。
8. 错误材料放入时能预判则直接拒绝；已合法放入但不再需要的内容可明确丢弃/清空，保留容器，零产物、零分、不隐式退回库存；垃圾处理不销毁设备。普通落地堆积、任意堆叠不引入。
9. 正常小关按营业与收尾自然完成，未满足订单不新增正常业务失败；保留现有固定分/星级作为已有能力，0 星可完成。失败重开技术入口不等于业务失败规则。
10. 固定伙伴继续使用既有单机询问/洗碗及 Level-local 成长规则。逐步增加可观察任务与人工接手，先不替换为自主全厨房 AI。最终目标不硬编码玩家专属工位。
11. 供应点区分无限取料与有限库存。包装与投料份分离；采购、到货、仓储和工位备料独立，不从候选配方直接推算采购量。
12. 布局仅准备态修改；普通工位可移动/旋转，占用按定义，交互面与通路一起校验。营业中不搬设备。房屋扩建先表达逻辑可用区域，不把价格、关号与永久权限写死。
13. 首批不引入过熟、火灾、故障、污渍、保质或经济惩罚。先完成恢复通路。冷却、融冰、封口、洗菜、逐个包饺子等不额外造工序。
14. 关卡允许内容 = 本关配置与已解锁权限的交集；曾解锁不代表每关都可用。菜单生成前必须检查供应、完整工序依赖、容器和设备可达能力闭合。
15. 暂不定正式价格、加工秒数、采购等待、每批产量。领域验收用显式 fixture 数值，标注“测试值”；时钟由固定 Tick 推进，不能给正式内容套一套未经测量的时间。

## 分阶段出口

命名空间：Task S01–S15 是单机工作编号，菜单 S01–S12 是甜品编号；引用时必须写“Task Sxx”或“菜单 Sxx”，二者不可当作同一身份。

| 顺序 | 出口意图 | 当前状态 |
|---|---|---|
| 单机 | 逻辑移动/目标、物件与加工、菜单依赖、独立订单绑定、前厅、供应/布局形成可重放可恢复闭环 | planning；已有受限纯 C# 增量复用，缺口见注册表 |
| 网络 | 同样的业务命令与状态，真实机器通信、争抢和断线恢复 | 只登记，架构冲突先审议，不执行 |
| 单机 Unity | 一个本地玩家可看懂并操作完整营业流程 | 条件性 planning，prohibited，先解除禁令 |
| 网络 Unity | 多人场景合作与网络异常下的表现一致 | 条件性 planning，prohibited，依赖前两类出口 |

单机纯 C# 出口只能证明逻辑与宿主闭环，不能证明镜头、按键、动画、高亮、空间体感顺畅。后者明确归 Unity 阶段的场景验收。
