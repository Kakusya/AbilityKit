# Research: S14 durable 成功基线契约

- Query: 既有MajorCheckpointStore format1升级typed恢复载荷、Restart Load、failure baseline与独立文件边界。
- Scope: internal；当前主工作树只读，不实施Host、不运行.NET。
- Date: 2026-10-02

## Findings

### 现有事实

`CookingMajorProgress.cs:184-197` format1把Kitchen.CanonicalText存为字符串；:200-241只恢复choices并返回KitchenCanonical，不能获得typed Recipe。CanonicalCheckpoint是私有展示结构，不是Recipe DTO，不能反序列化该字符串为CookingRecipeCheckpoint。

没有现成的 `CookingRecipeCheckpointCodec` Serialize/Deserialize；Recipe只有CanonicalText/Sha256。现成 `CookingLevelCheckpointCodec`（CookingLevelCheckpoint.cs:110-166）序列化真正typed Level checkpoint，format7并限制Recipe.SchemaVersion=5，但它面向Running/Preparing同关恢复，不应伪装Created成功基线成Running存档。现有JsonRequired用于份数、binding、provenance、allocation、worker、poses、schema、supply/origins，store应直接typed JSON序列化Recipe以保留这些校验，不能canonical反解析或自动填缺字段。

现有store Write在Locked后写`.next`→读取解码→File.Move overwrite（MajorProgress.cs:141-161）。是成功基线文件替换模式；不是已证明突然断电耐久，也没有目录fsync。本轮应明确故障模型为进程重启、IO/权限/写入截断/重命名失败，不宣称断电零丢失。

### 最小format2载荷与API建议（未实现）

增加 `CookingMajorBaselinePayload` typed record：源成功LevelScope、目标恢复LevelScope/next preparation、trusted config identity、preparation policy identity、trusted front identity、InstalledLayout（含normalized GeometrySeedPoses）、SuccessHandoff Recipe、choices(Decoration/Unlocks/CookFaster/Locked)。Scope与Recipe.Scope同match、epoch和目标Level明确，不能仅保存match三个字符串而猜要恢复哪关。

Major envelope真实FormatVersion=2、Payload、IntegritySha256；hash采用choices确定排序+typed Recipe.CanonicalText+layout canonical/seed+scope/identity字段完整覆盖。`ReadBaseline(expectedMatch)`返回typed Payload，解码与完整性通过仅代表文件结构合法，domain/host恢复仍须trusted factory验证；不得把读取成功叫加载成功。

`WriteBaseline(payload)`只被现有成功收口事务调用；store校验Locked、handoff裁剪结构、Recipe schema与match/scope一致、必需字段完整。业务成功许可由host验证，store不能靠caller随便填Success bool作为授权。不提供自动每Tick保存或任意时刻保存。旧Write/Read兼容接口可保留format1用于历史测试，但不能自动声称其typed可恢复；新WriteBaseline只写format2；ReadBaseline遇format1明示UnsupportedLegacyBaseline，不猜补seed/layout/供应。

不必新增公共RecipeCodec：store私有JsonSerializerOptions对typed Recipe做Serialize/Deserialize；如确实复用，新增独立 `CookingRecipeCheckpointCodec.cs` 提供受限字数+schema+hash envelope，仅序列化，不越权执行domain恢复。禁止给Recipe序列化借用LevelCodec包装出假生命周期。

### Restart Load完整链（Host归layout_check负责）

调用方提供存档目录、expected match及trusted config/factory → ReadBaseline format2/字段/hash/scope验证 → 用trusted factory按目标Level重建policy/front，比较记录identity且重新验证choices∩levelAllowed → stage恢复目标layout与seed，先安装有效几何 → AcceptSuccessHandoff typed Recipe恢复库存、份数、tombstones、allocator与供应 → trusted新前厅owner重建clock/伙伴计数 → 新generation在Created，调用BeginPreparation进入Preparing可备料，不自动Running → 全部成功后发布host与loaded progress。

加载失败返回明确reason和无host；不得覆盖文件、套用部分choices、用半截库存继续游戏。损坏基线不能退回错误版本并悄悄减少库存；显式新游戏标准baseline是另一个调用选择。Restart不重播原成功结算、奖励或供货receipt，不复制菜品，不调用opaque allocator验证旧ID。

### failure baseline语义

同进程正常失败重开：使用当前关trusted标准初始供应和供应配置，保留已确认major choices并按当关许可过滤/校验；不保留失败现场stock/在途货物/allocator水位/客户/伙伴成长，不写文件。进程重启加载：恢复最后一次成功写出的typed基线，而不是失败现场；pending delivery的RemainingTicks和finite供应数量来自成功基线，继续领取不能重复物化。两条路径必须分别测试，不能用“标准初始供应”替代成功基线剩余库存。

若保存发生在成功next generation的Created、选择Locked之后，文件应保存该真实目标Level/preparation/layout及carry recipe；不能把上一关scope同时当重启目标。提交次序与Host事务协调：candidate全验 → store temporary写验 → 原子文件publish成功 → next host publish；如host发布可失败，需要commit状态/恢复策略由Host实现明确处理，store不猜测已发布。禁止宣称文件与内存两资源天然原子。

### Fault matrix与独立测试

| 故障 | 预期 |
|---|---|
| 未Locked/非成功裁剪/字段缺失/schema不支持 | 拒绝，旧文件与progress不变 |
| format1/未知format/hash不符/截断/过大 | typed读取结构化拒绝，无半payload |
| wrong match/目标scope/配置policy身份不符 | 文件解码或Host trusted加载拒绝，不创建活host |
| .next写失败/读取失败/rename失败 | WriteFailed；旧成功文件仍可读；临时文件不被当current |
| 两次新store实例读回 | typed recipe canonical、layout seed和choices等价 |
| 供应部分领取/普通tombstone/opaque allocator | typed持久化字段保持，Host继续高于水位且不重分配历史ID |
| layout/seed/choices/supply篡改且重算外层hash | codec允许结构但Host trusted/domain仍拒绝，证明checksum非授权 |
| 成功文件加载后再失败 | failure重开遵循当关标准供应；文件仍最后成功基线 |

### root可独立负责文件边界

root可修改 `src/AbilityKit.Game.Cooking/CookingMajorProgress.cs` 的store/typed read API，或把新typed contract与codec放入 `CookingMajorBaseline.cs`；新增 `src/AbilityKit.Game.Cooking.Tests/CookingMajorBaselineStoreTests.cs`。不改CookingLevelEtHost/Preparing/Geometry/Recipe restore，避免layout_check所有权冲突。新API signatures应提前发送layout_check；host负责生成可信payload、实际load与跨关原子安装。新store codec测试不证明完整Restart验收，须Host生产路径后复合测试。

## Caveats / Not Found

所有新API为设计建议，未实现、未运行测试；format1无法typed恢复为已确认事实。没有访问implement/check manifests。新增文件边界沿用现有应用层store，不新增架构authority或anytime save。
