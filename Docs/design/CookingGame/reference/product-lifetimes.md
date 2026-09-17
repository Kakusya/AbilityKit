# Cooking 产品对象与生命周期参考

> 性质：设计参考，不是已实现能力或活动 task 的批准书。已确认项来自 owner 的产品审议；现有代码仍可能沿用不同术语，迁移必须由独立 task 规划。

## 1. 已确认的对象语义

### 1.1 Match

`Match` 是玩家建立联机协作关系后形成的长期逻辑容器，从协作关系成立持续到玩家明确解散。

已确认：

- Match 不是一条 transport connection。
- Match 不是一个 Level。
- Match 不是存档，也不是存档实例化后的餐厅运行态。
- Match 可以跨越多次存档选择。
- Match 内可以选择已有存档，也可以新建存档。
- Match 可以创建、暂停、恢复和结束独立 Level。
- Level 结束不自动结束 Match。
- Match 只有在玩家明确解散协作关系时才进入终止流程。

建议的生命周期词汇：

```text
Forming -> Active -> Dissolving -> Dissolved
```

该枚举尚未实现；具体状态与转换应由后续 Match task 定义和测试。

### 1.2 Connection

`Connection` 是单条 transport 连接，具有独立 `ConnectionId` 和比 Match 更短的生命周期。

已确认：

- 一个 Match 可以拥有多条 Connection。
- Connection 断开不应通过 ET 递归释放自动销毁 Match。
- Connection 与 Participant 的绑定是引用关系，不是身份等价关系。
- host 本地 Participant 不应被强迫依赖远端 Socket 的生命周期模型。

现有代码已经把 `ConnectionId` 和连接状态与 `MatchId` 分开；未来树必须保留该边界。

### 1.3 Participant

`Participant` 表示 Match 内的逻辑玩家成员，和持久化 Profile、单条 Connection 分离。

已确认的边界：

```text
Participant != Connection
Participant != Profile
Participant != SaveSlot owner identity
```

断线恢复、Participant 在断线后保留多久、重连和 host 迁移仍未决定。

### 1.4 Profile 与 SaveSlot

- `Profile` 表示某位玩家的长期进度所有者。
- `SaveSlot` 表示该 Profile 持有的一份持久化进度记录。
- Profile/SaveSlot 默认是存储层中的文件或可移植持久化记录，不因扫描本地目录、登录账号或进入主菜单而自动实例化为 ET Entity。
- 目标发布平台包括 PC 与 Android；存档 repository 必须通过平台适配器解析各平台惯用的应用私有持久化目录，领域层和 wire DTO 不得保存或比较绝对文件路径。
- 未联机时，当前本地账号对其 Profile/SaveSlot 的选择、读取和写入负责。
- 联机 Match 成立后，由参与者选择一份 Profile/SaveSlot 作为本次协作的进度来源；选中并校验后，才在 Match 内创建 session-scoped loaded-save 运行对象。
- loaded-save 运行对象不是原始文件本体，也不能把存储句柄或文件路径作为跨机器身份；它保存稳定 ProfileId、SaveSlotId、revision、configuration identity 和完整性信息。
- listen host 根据 loaded-save 创建 RestaurantRuntime，并对该 Runtime 的玩法模拟负责；持久化 Save 的身份、revision 和最终写回规则仍由 selected save contract 负责。
- Profile/SaveSlot 的持久化生命周期独立于 Match、Connection、RestaurantRuntime 和 Level。
- ET 运行时身份不得作为 ProfileId、SaveSlotId 或网络传输身份。

这形成三层不同的权威概念：

```text
Local/offline account authority
  -> authorizes access to locally held Profile/SaveSlot

Selected save authority in Match
  -> identifies the chosen progress baseline and revision

Listen-host runtime authority
  -> advances RestaurantRuntime/Level gameplay and emits settlement/save result
```

三者不得合并为“host 机器拥有存档”。更换 listen host 不应改变所选 ProfileId/SaveSlotId，也不应要求存档原本位于 host 机器。

## 2. 存档 Owner 与 listen host

### 2.1 已确认决定

存档 Owner 与 listen host 解耦。

- Match 可以选择任意 Participant 持有的存档。
- Owner 可以发出同平台 bearer 授权包；PC 与 Android 彼此隔离，授权包不能跨平台导入。任意合法持有授权包的同平台本地 Profile 都可以导入，并在导入成功时立即创建归自己的新 SaveSlot。
- 新 SaveSlot 归本次导入时接收设备上已认证/已选择的本地 Profile，该 Profile 立即成为新 Owner；这里的 Owner 是稳定 Profile 身份，不是物理机器或临时 listen-host 角色。
- 新分支保留来源 ProfileId、SaveSlotId、revision、完整性与 AuthorizationId，但不能写回来源 SaveSlot。
- 来源 Owner 的 Profile 不自动增加该分支，也不负责分支后续写入；新 Owner 可以在来源 Owner 不在线时独立发起 Match并继续推进自己的分支。
- 新分支与来源存档永久视为不同存档，可以同时存在、分别继续推进，禁止自动合并或覆盖来源存档。
- 分支必须记录 OwnerProfileId、ParentProfileId、ParentSaveSlotId、ParentRevision、RootProfileId、RootSaveSlotId 和 AuthorizationId 等谱系信息；谱系用于追踪和展示，不授予自动合并语义。
- 授权副本不授予接收者伪造来源 Owner 身份或绕过 revision/完整性校验的能力；多人 Match 中由谁确认创建分支以及授权撤销和安全验证仍由后续跨层契约明确。
- 存档可以传输给其他玩家的 listen host。
- listen host 校验传入数据后，实例化一份新的权威运行副本。
- listen host 不因运行副本而成为原始 SaveSlot 的持久化 Owner。
- 原始 SaveSlot 和权威运行副本必须具有不同身份。

推荐的数据流：

```text
selected loaded save
  -> listen host validates and instantiates RestaurantRuntime
  -> participants complete one Level successfully
  -> Runtime emits an immutable updated-save result
  -> every eligible participant may persist that result in its own local repository
  -> each persisted SaveSlot belongs to that participant's local Profile
```

已确认：Level 启动时冻结 eligible participant roster。只有该开局名单内的 Participant 可以领取本关成功更新；后来加入 Match 但未参加该 Level 的成员不能领取。名单内每人采用主动领取，不自动写入本地文件；某人未领取或暂时断线不影响其他人，也不回滚 Level 成功。

每个 `SaveResultId` 对每个 eligible `ProfileId` 的一次性只保证为每台设备的本地 repository 幂等一次。同一凭证复制到同平台另一设备后，可以再次领取并形成另一独立分支；无中心服务时不宣称 Profile 级全局唯一。

Level 成功提交时，host 为冻结 roster 中每人生成稳定凭证并发送；客户端持久化凭证后返回 ACK。未 ACK 不回滚成功或阻塞他人，host 在 Match 存续期间重复发送同一凭证，直到收到 ACK 或 Match 终止。

终止状态按客户端各自观察判定：收到并持久化 normal-close receipt 的客户端关闭本地未领取资格；没有收到回执的客户端按非正常终止处理，可以使用已经持久化的离线凭证。不同客户端可以对同一 Match 得到不同终止观察；无中心服务时，回执不能撤销其他设备上未观察到它的凭证复制件。Level 成功提交前的失败、崩溃或中断不产生可领取结果。

PC 与 Android 的 Profile、Match、SaveResult 和领取凭证彼此隔离，不能跨平台兑换或迁移。

领取时玩家可以选择新建 SaveSlot，也可以显式覆盖授权谱系内允许的现有 SaveSlot。覆盖目标可以不属于领取者 Profile，但必须通过目标身份、授权范围和 expected revision 校验；覆盖是有损替换，不是 merge，也不改变目标 SaveSlot 的 OwnerProfileId。

领取凭证只在 repository 原子提交成功后消耗，失败可重试。覆盖事务进行时必须保护旧记录，提交成功后不保留上一版本或用户可恢复备份。成功领取记录归领取凭证绑定的 Profile，并保证同一 SaveResultId/ProfileId 最多成功一次。

网络上传输稳定业务 DTO，禁止传输：

```text
Entity.Id
Entity.InstanceId
EntityRef<T>
```

传输契约未来至少需要区分：

```text
ProfileId
SaveSlotId
SourceRevision
SchemaVersion
ConfigurationIdentity
PayloadHash
IntegrityData
TransferId
```

这些字段是设计输入，不表示 wire schema 已经实现。

### 2.2 Save candidate 与 SaveSlot

Match 中公开的 `SaveCandidate` 是临时候选描述，不是原始 SaveSlot 本体。

```text
Profile
└── SaveSlot                  persistent record

Match
└── SaveCandidate             transient metadata/reference
      -> ProfileId + SaveSlotId + revision metadata
```

同一 Participant 可以公开多份候选，因此 SaveCandidate 倾向于多实例 Entity，而不是 Participant 的单例 Component。

### 2.3 选中后实例化，而不是默认 Entity 化

未选择的 Profile/SaveSlot 保持为 repository 中的持久化记录和轻量 metadata，不展开成 ET Entity tree。Match 选定一份存档并完成传输/校验后，创建一份 session-scoped loaded-save 对象，候选结构为：

```text
Match
├── SelectedSaveComponent
├── LoadedSaveComponent
│     LoadedSaveId / Owner / Revision / Integrity / Payload capabilities
└── RestaurantRuntimeComponent
      -> references LoadedSaveId / stable SaveSlotId
```

`LoadedSaveComponent` 的准确字段拆分仍需在实现 task 中收口，但以下边界已确认：

- 它是当前 Match 下零或一个的唯一从属对象，使用 `[ComponentOf]`/`Component` 后缀；
- 它是经过校验的运行期副本，不是原始持久化文件；
- 切换存档前必须先关闭当前 Runtime，再释放 loaded-save 对象；
- Match 解散时必须先处理允许的写回/导出，再释放 loaded-save 对象；
- 新 Match 必须重新传输或读取、校验并创建新的 loaded-save 对象。

### 2.4 LoadedSave、SaveResult 与 Claim 生命周期

已确认：

- 被 Match 选中、传输并校验后的存档使用唯一 `CookingLoadedSaveComponent` 表达，而不是多套一层唯一 Child Entity。
- 一个 Match 同时最多有一个 LoadedSaveComponent；切换存档时先结束/关闭 Runtime，再移除旧 LoadedSave Component。
- 每次 Level 成功原子提交后，在 Match 下创建一个 `CookingSaveResultEntity`，保存不可变共同进度结果和冻结 roster。
- 每个 eligible Profile 在该 SaveResult 下对应一个 `CookingSaveClaimEntity`，独立跟踪领取凭证、发送/重发、credential-persisted ACK、normal-close observation 和本地领取 receipt。
- SaveResult/Claim Entity 不能跨 Match 存活；Match 终止前只导出稳定 DTO/离线凭证，之后释放实体树。
- ParticipantEntity 与 ConnectionEntity 的生命周期都归属于 Match，但通过各自 Registry Component 成为间接子树，并使用引用 Component 相互绑定；Connection 断开或销毁不递归销毁 Participant。

正常解散顺序必须是业务收尾优先：停止新操作，结束/中止 Level，关闭并移除 Runtime Component，完成 SaveResult/Claim 导出和可观察回执，关闭 Connection，然后释放 LoadedSave、Result/Claim、Participant 和 Match。直接递归 Dispose 只用于异常资源回收，不能伪装成正常解散。

## 3. RestaurantRuntime

`RestaurantRuntime` 是 listen host 根据当前 Match 内已经校验的 LoadedSave Component 或新存档模板实例化的唯一权威餐厅运行 Component。

已确认：

- 一个 Match 同时最多绑定一份未关闭的 RestaurantRuntime。
- Match 可以在没有 RestaurantRuntime 时存在，例如等待选择、传输或校验存档。
- 切换到其他存档或新建存档前，必须先显式关闭当前 Runtime。
- 不允许一个 Match 同时保留多份暂停 Runtime。
- 同一 SaveSlot 每次加载都产生新的 RestaurantRuntime identity。
- Level 不直接修改原始持久化 SaveSlot；它修改 Runtime，成功结算后再形成持久化结果。

切换 Runtime 的顺序：

```text
end/abort current Level
  -> resolve or discard allowed staged changes
  -> close current RestaurantRuntime
  -> dispose its runtime tree
  -> select/create another SaveSlot
  -> transfer and validate
  -> instantiate a new RestaurantRuntime
```

### 3.1 Runtime 不跨 Match

已确认：活 `RestaurantRuntime` 不能跨 Match 迁移，Runtime 的完整生命周期只能属于创建它的 Match。

- `CookingRestaurantRuntimeComponent` 是 `CookingMatchEntity` 下零或一个的唯一从属 Component。
- Match 解散前必须先停止并收尾当前 Level，再关闭并移除 Runtime Component。
- Match Dispose 会递归释放仍属于它的 Runtime Component 子树，但 Dispose 不能替代存档、settlement 或 staged progress 的显式收尾。
- 活 Runtime Component 不能从 Match A 迁移或复制为 Match B 的活状态。
- 跨 Match 延续只允许传递 SaveSlot 数据或未来明确定义的 Checkpoint DTO。
- Match B 导入数据后必须创建新的 RestaurantRuntime identity、Runtime Component、命令水位和同步 baseline。

```text
Match A
└── RestaurantRuntime A
      -> export validated Save/Checkpoint
      -> explicitly close Runtime A
      -> dispose Match A tree

Match B
└── import Save/Checkpoint
      -> create RestaurantRuntime B
```

可以跨 Match 的身份/数据：

```text
ProfileId
SaveSlotId
validated Save payload
future CheckpointId / Checkpoint payload
```

不能跨 Match 复用：

```text
RestaurantRuntimeId
Level runtime identity
ET Entity.Id / InstanceId / EntityRef<T>
pending command queue
snapshot/batch watermarks
live Connection or Participant bindings
```

## 4. Level

`Level` 是独立玩法实例，与 Match 解耦。一个 Match 可以在其生命周期内创建和结束多个 Level。

已确认：

- 一个 RestaurantRuntime 同时最多有一个未结束的 Level Component。
- 不支持同一 Runtime 下多个并行 Level。
- 不支持同一 Runtime 下同时保留多个暂停 Level。
- Level 可以独立创建、启动、暂停、恢复和结束。
- Pause 保留同一个 Level Component 及其全部权威状态。
- Pause 关闭 fixed Tick 和 gameplay command admission。
- Ended Level 不复活；重试或下一关创建新的 Level identity，并重新安装/初始化 Level Component。

建议并已确认的 canonical Level 生命周期：

```text
Created -> Preparing -> Ready -> Running <-> Paused -> Ending -> Ended
```

`Success`、`Failed`、`Aborted` 是 Ending/Ended 的 Outcome，不扩展为互斥主状态。

Level identity/scope 明确包含：

```text
MatchId
RestaurantRuntimeId
LevelId
LevelEpoch
```

- `LevelId` 表示产品关卡身份；
- `LevelEpoch` 区分同一关卡的运行代际；
- 失败重试复用同一 LevelId，但必须增加 LevelEpoch；
- 成功进入下一关使用新的 LevelId 和更高 LevelEpoch；
- parent Match 与 Runtime 身份不得从旧 `CookingScope.Match` 隐式推导。

Pause 保留同一个 Level Component、Kitchen、时钟水位和已经准入但尚未执行的命令队列；Resume 后这些命令继续按原 batch/稳定顺序执行。Paused 状态关闭 fixed Tick 和新 gameplay command admission，不能在暂停期间继续扩张队列。

### 4.1 Pause

暂停期间保留：

- 当前逻辑 Tick；
- Kitchen 和 Process 状态；
- 当前订单、目标和关卡时间；
- command batch watermark；
- snapshot/event watermark；
- pending disposition history；
- 暂停前已经准入但尚未执行的 live queue/frozen batch。

Paused 状态不接收新的 gameplay command；新到达命令得到结构化 paused rejection，不占队列容量。Pause/Resume 不改变 LevelEpoch、HostFrameSequence、LogicalTick、batch/snapshot/event watermark，只是停止推进。

暂停前已经准入但尚未执行的 live queue/frozen batch 保留。Resume 后它们继续进入稳定仲裁和普通领域校验；不会因为暂停而自动接受，也不会跳过 stale item/version/scope 检查。

失败进入 Ending 时，旧 LevelEpoch 的全部 pending/frozen commands 必须取消并返回 ordered disposition；新 Epoch 绝不执行旧命令。失败重试复用 LevelId、增加 LevelEpoch，重建 Level-local LogicalTick、batch/snapshot/event watermark；Match/Application 级 HostFrameSequence 保持单调不重置，Scope+Epoch 区分运行代际。

## 5. Kitchen 与 Level 的关系

已确认：`Kitchen` 属于 RestaurantRuntime，并与 Level 并列，不属于 Level。

```text
RestaurantRuntime
├── Kitchen
└── Level                  zero or one unfinished instance
```

原因：食材、半成品、容器内容和加工进度需要在成功 Level 之间延续；如果 Kitchen 是 Level Child，Level Dispose 会错误销毁需要延续的现场。

### 5.1 Level 成功

已确认行为：

```text
stop Level Tick
  -> settle Level-specific orders/objectives
  -> persist only after successful completion
  -> dispose ended Level
  -> keep Kitchen state
  -> enter intermission/upgrade
  -> create next Level
```

Kitchen 保留：

- 食材与半成品；
- 容器内容；
- 现场位置；
- 未完成 Process；
- elapsed progress；
- Runtime 级实例/产品身份水位。

Level 专属内容重建：

- 订单；
- 目标；
- 关卡时间；
- 本关得分/结算上下文；
- Level command batch；
- Level snapshot watermark。

### 5.2 Level 失败重试

已确认行为：

```text
stop Level Tick
  -> no persistence write
  -> dispose failed Level
  -> discard failed Kitchen scene state
  -> rebuild Kitchen from the Level definition's standard initial supplies
  -> create a new Level runtime identity and greater epoch
```

产品关卡定义可以相同，但运行实例必须不同。

### 5.3 Level 之间升级

已确认行为：

- 升级发生在 Level 之间；
- 加工时钟暂停；
- 未完成 Process 自动迁移到升级后的 Station；
- elapsed progress 保留；
- 迁移必须作为后续独立原子契约设计和验证。

Process 与 Station 是领域引用关系，不能依赖 ET reparent 表达迁移。

## 6. Kitchen/Level 运行实体

已确认：

- 可携带锅、盘等容器是具有容器能力的 Item，不是与 Item 并列的第二身份。ET 中使用 `CookingItemEntity` 加可选 `CookingContainerComponent`；Item 仍只有一个权威位置。
- 固定 Station 是 Kitchen Child Entity。
- 未完成 `CookingProcessEntity` 是 Kitchen 的直接 Child，不是 Station Child。它通过稳定引用关联 Station、Input Item 和 Recipe，因此升级工位时只迁移引用并保留 elapsed progress，不执行 ET reparent。
- 每个运行时 `CookingOrderEntity` 是 Level Child，订单按小关结算并随 Level 释放；`CookingOrderBookComponent` 只负责索引和生成策略。
- Match Participant 跨多个 Level 存活；Level 启动冻结 roster 后创建 `CookingLevelParticipantEntity`，引用对应 Match Participant 并保存本关角色、avatar、资格和结果关系。Level End 释放 LevelParticipant，不影响 Match Participant。

## 7. 当前代码术语迁移提醒

现有 `CookingMatchLifecycle` 包含 LevelId、MapId、Layout、gameplay simulation 创建和玩法 Start/End，因此它更接近这里定义的 Level 生命周期，而不是新确认的长期 Match 语义。

现有名称包括：

```text
CookingMatchLifecycle
CookingMatchPreparation
CookingMatchLifecycleSnapshot
CookingMatchState
```

后续迁移策略已确认：

- 新增 canonical `CookingLevelLifecycle` family，产品 Level 的 Prepare/Start/Pause/Resume/End/Retry 与 fixed-tick admission 只以它为准。
- 现有 `CookingMatchLifecycle*` 保留为过渡兼容 facade，标记 obsolete，并委托给同一个 Level lifecycle 实现；禁止复制第二套状态机或双写状态。
- 旧 focused tests 和历史调用在兼容期继续作为 regression；新增测试以 Level 命名和语义为主。
- 当前 task 不全局重命名 `CookingScope`、`MatchId`、LAN/UDP 或 persistence 合同；旧 scope 只允许停留在兼容边界。
- 后续删除兼容 facade 必须由独立迁移 task 证明调用方与测试已清零。

不得继续因为旧类型叫 Match，就把 Level 和联机 Match 绑定为同一生命周期。

## 8. 尚未决定的产品边界

以下内容不得从本文推导默认答案：

- Participant 断线后的保留与重连策略；
- host 退出和 host migration；
- 暂停时 pending command 的具体 disposition；
- 存档传输的安全、签名和冲突解决协议；
- Save Owner 离线时结算如何确认和回写；
- Match 解散时未提交 Runtime 变化的 UX/策略；
- 真实时间 Tick 频率和 catch-up 策略。
