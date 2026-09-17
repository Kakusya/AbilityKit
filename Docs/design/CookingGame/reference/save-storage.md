# Cooking PC/Android 存档与授权副本参考

> 性质：设计参考，不表示 durable store、授权协议、云同步或 Unity 平台适配器已经实现。当前 Cooking Unity 仍禁止；本文记录目标平台和后续跨层契约必须遵守的边界。

## 1. 已确认目标

- 目标平台包含 PC 与 Android，但两个版本是彼此隔离的产品生态。
- PC 与 Android 不互联，不共享 Match、Profile 身份、SaveSlot、授权副本、save-result 或领取凭证，也不支持跨平台导入/导出。
- 两个平台分别使用各自惯用的应用私有持久化目录和独立 repository；共享纯 C# 领域规则不表示共享用户数据。
- Profile/SaveSlot 默认是持久化文件或可移植记录，不默认实例化为 ET Entity。
- 存档 Owner 与 listen host 解耦。
- Owner 可以授权其他玩家持有可独立开局的存档副本。
- 有效授权副本可以在原 Owner 不在线时用于创建 Match、选为 loaded save 并实例化新的 RestaurantRuntime。
- listen host 变化不能导致存档身份丢失，也不能要求存档原本位于 host 机器。

## 2. 三层边界

```text
Pure Cooking domain
  ProfileId / SaveSlotId / Revision / envelope validation
            |
            v
Pure C# repository
  logical keys, read, stage, atomic commit, enumerate metadata
            |
            v
Platform storage-root adapter
  PC or Android application-private persistent root
```

领域层、ET Entity、快照和 wire DTO 禁止持有：

```text
absolute file path
Unity Application.persistentDataPath value
Windows drive letter
Android package filesystem path
FileStream or storage handle
```

跨层只使用稳定逻辑身份：

```text
ProfileId
SaveSlotId
Revision
SchemaVersion
ConfigurationIdentity
PayloadHash
Authorization metadata
```

## 3. 平台位置约定

### 3.1 PC

默认存储位置应使用宿主/引擎提供的当前应用私有持久化数据目录，而不是硬编码：

```text
C:\... fixed path
Desktop
Documents
installation directory
Unity Assets/StreamingAssets
current working directory
```

未来 Unity PC 宿主获准后，平台 adapter 可以使用 `Application.persistentDataPath` 作为根目录。具体公司名、产品名和最终文件夹由正式产品配置决定，参考文档不写死。

### 3.2 Android

默认存储位置应使用 Android 应用专属持久化目录，由宿主/引擎解析；不得硬编码 `/sdcard` 或某个 package path。

未来 Unity Android 宿主获准后，同样由平台 adapter 使用 `Application.persistentDataPath`。应用代码不得假设其物理路径固定，也不得把路径写入 Save payload。卸载、系统清理、设备迁移和平台备份策略必须视为独立产品/平台行为。

### 3.3 可移植副本

应用私有目录适合默认存档，但“授权其他玩家持有副本”需要同平台内的显式导出/导入或联机传输能力。可移植副本是带身份、revision、完整性和授权信息的 envelope，不等同于复制一个内部绝对路径。

PC 授权副本只在 PC 生态内使用；Android 授权副本只在 Android 生态内使用。两个平台不得互相导入 SaveSlot、授权包、save-result 或领取凭证。

默认存储与用户导出是两个不同入口：

```text
repository internal record
  != user-selected export artifact
```

Android 的文件选择器/分享入口、PC 的导入导出 UI 和 Unity 平台集成都属于未来获准的宿主工作，不在当前 task 实施。

## 4. 推荐纯 C# repository 契约

后续 durable-store task 应建立不引用 Unity API 的接口，概念上覆盖：

```csharp
public interface ICookingSaveRepository
{
    SaveMetadataListResult List(ProfileId? owner);
    SaveReadResult Read(ProfileId profile, SaveSlotId slot);
    SaveCommitResult TryCommit(
        ProfileId profile,
        SaveSlotId slot,
        long expectedRevision,
        CookingSaveEnvelope envelope);
}
```

准确签名、同步/异步模型、删除、备份和 quarantine API 需在该 task 中审议。核心契约必须包括：

- owner/profile/slot 作用域读取；
- schema/configuration/integrity 验证；
- expected revision compare-and-commit；
- staged write；
- 原子替换或等价的中断安全提交；
- 写入失败后旧记录仍可读取；
- 损坏记录不得静默退回默认存档；
- 结构化错误，不以路径字符串作为领域错误。

平台层单独提供根目录：

```csharp
public interface ICookingStorageRootProvider
{
    string GetPersistentRoot();
}
```

该接口只存在于 repository/host 边界，返回值不得进入领域模型、ET tree、snapshot 或 network payload。

## 5. 逻辑目录与文件身份

Repository 可以在平台根目录下使用固定的产品相对布局，但不能依赖用户显示名作为唯一键。候选逻辑布局：

```text
<cooking-save-root>/
  profiles/
    <ProfileId>/
      profile-metadata
      slots/
        <SaveSlotId>/
          current
          authorization-metadata
```

这只是逻辑布局参考；编码格式、扩展名和 manifest 拆分尚未批准。

必须满足：

- 路径片段由经过验证/编码的稳定 ID 生成；
- 禁止 `..`、绝对路径注入和用户文本直接拼接；
- 临时文件与目标文件位于同一可原子替换的存储范围；
- 成功提交前不能删除唯一有效旧版本；
- 枚举 metadata 不自动展开为 ET Entity。

## 6. 写入安全

现有 Cooking limited contract 已使用 JSON envelope、format version、128 KB 上限和 SHA-256 完整性，并验证 prepare/commit、写入中断和旧记录保留；这只是当前纯 .NET 基线，不等于 durable file store 已完成。

未来文件实现至少需要：

```text
serialize + validate candidate
  -> write sibling temporary file
  -> flush according to approved durability policy
  -> atomically replace current record
  -> verify committed record/readability
  -> discard superseded content after successful commit; no retained user-restorable previous version
  -> report committed revision
```

禁止：

```text
delete current
  -> try to move temporary file
```

因为 move 失败时会同时失去旧记录和新记录。

## 7. 授权副本、成功结果与存档分支

授权副本传输时携带来源存档及授权基线：

```text
SourceProfileId
SourceSaveSlotId
SourceRevision
PayloadHash
Owner identity
Authorization identity
Authorization scope
Authorization version/expiry/revocation data
```

已确认采用“bearer authorization + 导入即分支”语义：

- 原始 SaveSlot 的授权包不预先绑定 RecipientProfileId；同一平台内任何合法持有该授权包、且在本地选择了接收 Profile 的玩家都可以尝试导入。
- 授权包一旦泄露，其他持有者也可能导入并创建各自分支；这是 bearer 模型的明确代价，不能把文件占有者误写成来源 Owner。
- 授权副本成功写入接收设备的本地 repository 时，就立即创建新的存档分支；不等待独立进度、开局或首次成功结算。
- 新分支获得新的 `SaveSlotId`，归接收设备上执行本次授权导入的本地 `ProfileId`；该本地 Profile 成为新 SaveSlot 的 owner。
- 这里的 owner 是稳定 Profile 身份，不是物理机器，也不是临时 listen-host 角色。玩家以后更换设备或不再担任 host，不改变该 SaveSlot 的 OwnerProfileId。
- 如果当前设备没有已认证/已选择的本地接收 Profile，repository 不得完成授权导入。
- 副本接收者不能冒充来源 Owner；新分支通过谱系和 AuthorizationId 证明其来源。
- listen host 使用该分支开局时，仍须校验新分支自己的 owner、revision、完整性以及来源授权谱系。
- 每次 Match 都创建新的 LoadedSave Component 和 RestaurantRuntime Component；ET Component/Entity identity 不进入授权文件或新分支。
- 新分支不能继续写回或覆盖来源 `SaveSlotId`。
- 来源存档和所有导入分支可以同时存在并继续独立推进。
- 分支之间永不自动合并，也不提供货币、解锁、奖励或经营进度的字段级合并。
- 一个分支不能通过较高 revision、较晚时间戳或 host 身份自动取代另一个分支。
- 每次 Level 成功并产生持久化更新时，权威 Runtime 应输出一份与本次成功结果对应的不可变 save-result artifact；该 artifact 不是某一台机器的文件路径或 SaveSlot。
- Level 启动时冻结本关的 eligible participant roster；只有该名单内的 Participant 可以领取本关成功结果，之后加入 Match 但未参与本关的成员不能领取。
- 名单内参与者在成功时暂时断线或尚未领取，不影响其他参与者领取，也不改变不可变 save-result artifact。
- 合资格参与者必须主动领取；系统不因 Level 成功就在所有设备上自动写入 SaveSlot。
- Level 成功原子提交时，host 生成不可变 SaveResult，并为冻结 roster 中每个 eligible Profile 生成对应领取凭证。
- host 向各客户端发送其 SaveResult/凭证；客户端必须先把凭证持久化到本地 repository，再返回 credential-persisted ACK。
- 未收到 ACK 不回滚 Level 成功，也不阻塞其他参与者，但 host 在 Match 存续期间保留并重发该参与者的同一凭证；重发必须使用稳定身份并保持幂等。
- Match 终止后 host 可以停止重发。已持久化凭证按客户端观察到的 normal-close/non-normal 规则处理；从未收到并持久化凭证的客户端不能凭空恢复资格。
- 同一个 `SaveResultId` 对同一个 eligible `ProfileId` 的“一次领取”只保证为每台设备本地 repository 幂等一次。
- 在无中心服务条件下，如果同一个 Profile 的领取凭证被复制到同平台的另一台设备，两台设备可能各自成功领取一次；每次成功领取形成各自独立的本地 SaveSlot/分支，不自动合并，也不宣称同平台全局唯一。
- 同一设备上的重复请求必须返回同一本地领取记录/结果，不得再次创建或覆盖。
- 每名合资格参与者在 Level 成功提交时获得与其 eligible ProfileId 关联的领取资格；这与原始 SaveSlot 的 bearer authorization 是两种不同凭证，不得混用。
- Match 是否正常解散由每个客户端自己的可观察协议结果判定，不要求所有客户端得到同一终止观察。
- 客户端收到并持久化有效的 normal-close receipt 后，本地尚未领取的资格关闭，之后不得领取。
- 客户端没有收到 normal-close receipt（包括断线、进程退出、网络故障或 host 异常终止）时，按该客户端观察到的非正常终止处理，保留已经持久化的未领取凭证。
- 同一个 Match 可以在已收到正常回执的客户端上表现为正常解散，而在未收到回执的客户端上表现为非正常终止；这是 client-observed 方案接受的结果。
- normal-close receipt 只能关闭实际观察并持久化该回执的本地凭证副本；在无中心服务、完全离线的条件下，不能宣称撤销其他设备上未观察到该回执的复制件。
- Level 成功提交前的崩溃、失败或中断不产生 SaveResult，也不产生可领取进度。
- 离线领取凭证只能在同一平台生态内使用；PC 与 Android 不能互换。
- 领取时由玩家选择：创建新的 SaveSlot，或显式覆盖授权谱系内允许的现有 SaveSlot。覆盖是有损替换，不是存档合并。
- 覆盖目标不要求必须归领取者 Profile 所有，但必须位于本次授权允许的 parent/root 谱系范围内，并通过目标 Profile/SaveSlot、授权权限和 `expectedRevision` 校验；仅知道谱系 ID 不构成覆盖权限。
- 覆盖不会改变目标 SaveSlot 的 `OwnerProfileId`。领取者、领取凭证绑定 Profile 与目标 SaveSlot Owner 可以不同；事务只替换目标内容、revision 和 provenance metadata。
- 领取凭证只在新建/覆盖事务原子提交成功后标记为已使用；身份、授权、完整性、revision、容量或磁盘写入失败均不得消耗资格，允许重试。
- 同一台设备的本地 repository 对同一个 `SaveResultId + eligible ProfileId` 最多保存一个成功领取记录；提交成功后的重复请求必须返回该记录，不得再次创建或覆盖。
- 覆盖成功后不保留被覆盖内容的上一版本/用户可恢复备份；提交完成前仍必须保留旧记录，只有新记录完成原子替换并验证成功后旧内容才可消失。

### 7.1 共同进度结果与本地 SaveSlot

建议后续正式契约区分：

```text
SaveResultId / ProgressGenerationId
  immutable authoritative result produced after a successful Level

SaveSlotId
  locally owned persistence record under one Profile
```

同一个成功结果可以被多个参与者分别持久化，每个 Profile 只能成功领取一次，但每位玩家可以选择新建或覆盖其允许的本地 SaveSlot：

```text
Successful Level -> ProgressGeneration G17

Alice/Profile-A claim G17 once -> create SaveSlot-A or overwrite selected local slot
Bob/Profile-B   claim G17 once -> create SaveSlot-B or overwrite selected local slot
Carol/Profile-C claim G17 once -> create SaveSlot-C or overwrite selected local slot
```

如果 Bob 之后独立游玩，则从其领取后的本地 SaveSlot 继续产生后代结果；Alice 和 Carol 的存档不受影响。覆盖某个现有 SaveSlot 不会把被覆盖内容与 G17 合并。准确命名、离线凭证格式、覆盖目标限制、恢复策略和 wire payload 需由后续 save-distribution task 定义。

### 7.2 分支谱系

每个分支应保存不可变来源信息，概念上至少包括：

```text
SaveSlotId                 new identity allocated at successful import
OwnerProfileId             receiving local profile selected during import
ParentProfileId            owner profile of the immediate source slot
ParentSaveSlotId           immediate source slot
ParentRevision             exact source revision
RootProfileId              owner profile at the lineage root
RootSaveSlotId             lineage root
BranchCreatedBy            receiving local ProfileId; equals OwnerProfileId
BranchCreatedAt            import diagnostic metadata, not conflict authority
AuthorizationId            authorization validated during import
```

谱系只用于追踪来源、展示和审计，不能用于自动合并。

```text
SaveSlot A @ revision 15
├── SaveSlot A @ revision 16...       original line continues
├── SaveSlot B @ revision 1...        branch from A@15
└── SaveSlot C @ revision 1...        another branch from A@15
```

B、C 与 A 是三个不同存档，可以同时存在。B 后续再次分支时会生成新的 SaveSlot D，并记录 `ParentSaveSlotId = B`。

### 7.3 Repository 提交语义

Repository 必须区分：

```text
Advance existing slot
  -> compare-and-commit expected revision on the same SaveSlotId

Import authorized branch
  -> require a selected receiving local ProfileId on the same platform ecosystem
  -> validate source profile/slot/revision + bearer authorization
  -> allocate a new SaveSlotId under the receiving ProfileId
  -> record parent/root lineage and authorization identity
  -> atomically write revision 1 of the new slot to the receiving local repository
  -> never mutate the source slot or source owner's profile

Claim successful result
  -> validate durable claim credential against SaveResultId + RecipientProfileId
  -> return prior receipt for an already-successful claim
  -> player chooses create-new or an existing target SaveSlot within the authorized lineage
  -> create: allocate new SaveSlotId under RecipientProfileId
  -> overwrite: validate target profile/slot, authorization scope and expected target revision
  -> stage and atomically commit without merge semantics
  -> preserve the old target during the transaction, but retain no previous-version backup after success
  -> record result/claim/provenance metadata
  -> mark entitlement consumed only after the final repository commit succeeds
```

来源 revision 已经过期不应自动覆盖来源存档。是否仍允许基于历史 revision 创建分支取决于授权有效性和未来产品策略，但即使允许，也只能创建新 SaveSlot，不能写回来源。

尚未决定：

- bearer 授权如何签发和验证，以及泄露后的风险提示；
- SaveResult/领取凭证的持久化 ACK、重发队列、Match 终止清理和进程崩溃恢复协议；
- normal-close receipt 的签名、持久化、乱序和重复处理协议；
- 是否需要同平台云端/中心服务；
- 同平台不同设备间如何传输授权密钥或凭据；
- 加密和密钥保存策略。

在这些问题解决前，SHA-256 只能视为损坏检测，不能宣称能够证明授权者身份或抵抗恶意篡改。

## 8. 验证要求

未来 repository 和平台 adapter 至少应覆盖：

- 注入临时根目录的纯 .NET 测试；
- PC/Android root-provider 合同测试；
- 同一 ProfileId/SaveSlotId 在不同物理路径下保持同一业务身份；
- 正确 revision 成功推进现有 SaveSlot；
- stale expected revision 拒绝且不覆盖现有 SaveSlot；
- 从指定 ParentSaveSlotId/ParentRevision 创建分支时分配新 SaveSlotId，来源存档保持字节级和 revision 不变；
- 原存档与多个分支同时枚举、读取和独立提交；
- 分支谱系可追踪但不存在 merge API 或隐式 winner；
- 截断、损坏、错误 owner/config/schema/integrity 拒绝；
- 中断或替换失败后旧记录仍可读取；
- 非法路径片段拒绝；
- 同一设备的 `SaveResultId + ProfileId` 本地幂等一次；复制到另一同平台设备后允许再次领取并产生独立分支；
- Level 成功后 host 对未 ACK 凭证执行稳定重发，客户端持久化后 ACK；
- normal-close receipt 已持久化与未观察到回执的客户端分歧场景；
- bearer 授权包的 valid/invalid/unknown-authority 场景；
- 不把 PC 路径或数据 artifact 写入 Android payload，反之亦然。

真实 PC/Android 文件系统、权限、卸载/备份行为和 Unity `persistentDataPath` 集成，只有在对应 Cooking Unity 平台范围重新获准并实际执行后才能作为通过证据；两个平台必须分别验证，不得以其中一个平台通过代替另一个平台。
