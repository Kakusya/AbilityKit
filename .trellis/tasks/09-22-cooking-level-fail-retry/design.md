# 小关失败重开：技术设计

需求见 [prd.md](prd.md)。本设计不定义失败条件，不写盘，不改成功交接，也不改同代际恢复。

## 决定

失败重开复用现有 `CreateRetry`，不新增第二套代际 API。身份规则已经成立：同一 `LevelId`、更高 `LevelEpoch`、同一 Match 与 RestaurantRuntime，源代际保持 `Ended`。缺的只是厨房。

重开厨房是一份新仿真加上现有标准初始供应，不是把失败厨房裁成 checkpoint。`ExportSuccessHandoff` 和 `RestoreCheckpoint` 都不参与这条路径。

`Docs/Todo.md` 里「关闭失败 Match，创建新 MatchId」不采用。产品参考写明失败重试不结束 Match。

## 边界

| 层 | 责任 | 不负责 |
|---|---|---|
| `CookingContentCatalog` | 继续提供唯一的标准初始供应摆放 | 判断失败、持有代际 |
| `CookingLevelLifecycle` | 继续只判定能不能重开；新代际在 `Start` 时绑定调用方已经摆好的厨房 | 自己读取内容文档、自己清空失败现场 |
| `CookingLevelEtHost` | `Failed` 收口后丢掉失败厨房；`CreateRetry` 安装下一代、创建新仿真、摆上标准供应，并停在 `Created` | 自动 `Prepare` / `Start`、写盘、成功交接 |

领域测试可以直接「新建仿真 + `ApplyStandardInitialSupply`」作为对照臂。宿主测试必须走 `CreateRetry`，不能只测这个纯函数。

## 数据流

```text
源代际 Ended + Failed
  -> 现有身份校验（同一 LevelId，更高 epoch，只能一次）
  -> CompleteEnd 已经 ReleaseSimulationOwnership，失败厨房不进入下一代
  -> 安装下一代 lifecycle，状态停在 Created
  -> 用现有工厂创建一份新 CookingRecipeSimulation
  -> ApplyStandardInitialSupply(新仿真, 本关 CookingContent)
  -> 把这份厨房挂到下一代，标记为重开厨房
  -> 取消源宿主尚未执行的命令
  -> 命令水位归零，HostFrameSequence 不回退
```

任一步失败则整笔拒绝：不安装可观察的新代际，不标记源代际已创建下一代，不留下半摆好的厨房。身份校验失败时，失败厨房的 canonical 不变。

## 契约

### 厨房绑定

成功交接已经有 `ReceivesSuccessorKitchen`：`Created` 代际可以在 `Start` 之前持有一份厨房，`Start` 绑定它，不再向工厂新建。

失败重开复用这个绑定，不新增「重开厨房」标志。两条路径的差别只在厨房内容：

- 成功：同一份仿真，先 `AcceptSuccessHandoff`，再挂到下一代。
- 失败：工厂新建一份仿真，`ApplyStandardInitialSupply` 之后再挂到下一代。

因此 `Start` 的现有约束保持不变：标志为真且厨房为空，拒绝 `GameplayUnavailable`；标志为真时不得 `CloseLifecycle` 这份厨房。绑定之后，准备态仍然不能改厨房，也没有新的 mutation API。

### 宿主入口

`CookingLevelEtHost.CreateRetry` 在现有 `InstallGeneration(candidate, isRetry: true)` 成功之后补厨房，而不是把厨房留到下次 `Start`。

顺序：

1. 现有 `TryCreateRetryCandidate` 先校验。失败则零变更，不创建仿真。
2. 源宿主在 `Failed` 的 `CompleteEnd` 之后不得再持有失败厨房。今天这条已经成立，保持不变。若重开时仍能看见源厨房，先释放再安装；不能把源厨房交给候选。
3. `InstallGeneration` 成功后，候选仍是 `Created`。不调用 `Prepare`，不调用 `Start`。
4. 用候选自己的 gameplay factory 创建新仿真。工厂失败则宿主进入 fault，不把空厨房报成重开成功。
5. 对这份新仿真调用 `ApplyStandardInitialSupply`。供应来自构造本关时已经校验过的 `CookingContent`，不在重开时再读第二份文档。
6. `AdoptSuccessorKitchen` 把新仿真挂到候选。挂不上则释放新仿真并 fault，不留下无主厨房。
7. 宿主命令水位归零，pending 按现有 `level-generation-replaced` 取消。`HostFrameSequence` 单调不重置。

新仿真在 `Created` 期间不被 fixed tick 驱动，也不接受 gameplay 命令。这是现有 `LevelNotRunning` 拒绝，不新增原因码。

### 不触碰的路径

- `CreateSuccessor` / `ExportSuccessHandoff` / `AcceptSuccessHandoff` 保持成功交接语义。`Failed` 不能从这里把现场留下。
- `ExportCheckpoint` 仍要求 `Running` 且 pending 为空。失败重开不导出、不恢复。
- `CookingPersistence*` 的 prepare / commit / read 不进入 `CreateRetry`。
- 指纹金样不改。`CreateRetry` 不产生新的命令 wire 形状。

## 可观察结果

现有 `CookingLevelHostGenerationResult` 继续表达源 scope、新 scope、源 outcome 和版本。重开成功时：

- `NewScope.Level` 等于源 `LevelId`；
- `NewScope.LevelEpoch` 严格更高；
- 候选 `State` 为 `Created`；
- 候选厨房的物品与干净碗池等于对照臂；
- 订单、结算、加工为空，逻辑 Tick 为 0。

不把「保留加工数 / 清除订单数」拿来描述重开。那三个计数字段是成功交接的观察，失败重开保持默认 0。

## 风险

- 工厂在 `InstallGeneration` 之后才创建厨房。安装已经提交时，工厂或供应失败不能假装零变更。这条和成功交接里「交接失败即 fault」同一口径：身份已经换代，厨房没摆好就 fault，不把半截重开交给调用方。
- `ApplyStandardInitialSupply` 对未知 location 会抛异常。内容目录已经过 v2 校验，重开不再吞掉这个异常去返回一个空厨房。
- 领域生命周期自己没有 `CookingContent`。领域验收用对照臂证明供应规则；宿主验收证明 `CreateRetry` 真的摆上了这份供应。不要把内容文档读进 `CookingLevelLifecycle`。
