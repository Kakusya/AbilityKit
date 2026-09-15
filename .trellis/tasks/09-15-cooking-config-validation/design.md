# P3 数据配置验证：迁移设计

> 本文件是遗留规划的设计迁移，不表示设计已经批准或代码已经实现。

## 当前受限实施记录

本任务已开始一轮受限的纯 .NET 实施。当前已存在 P2 fixture 形状仅含 ItemDefinition、Appliance、Recipe、Container 与单一 `ProcessId` 引用，因而实现了应用层 `CookingConfigurationRegistry`：候选批次先执行全量 definition/reference/capability/容量诊断，全部通过后才将不可变快照替换为当前配置；失败候选保留旧快照及 identity。identity 采用显式 schema、稳定表/记录/字段排序的 canonical JSON 与 SHA-256，不包含 runtime instance、Match、订单进度或 runtime snapshot。

实际覆盖 C01、C02、C04、C05、C08 以及 C07 的“无批准迁移只返回 blocked、绝不转换”分支。C03 仍 blocked：当前没有 Process graph、拓扑边或 Level 形状，不能为补齐矩阵而虚构 schema。C06 仍 blocked：P1 只有 transport-neutral session seam，本轮没有接入 real host/client gameplay binding 或 LAN。正式内容、工具格式、Unity、通用 ConfigDatabase、生产 transport 与旧数据实际迁移均不在本轮范围。

## Context

见 [proposal.md](proposal.md) 与 [spec](specs/cooking-config-validation/spec.md)。现有 `ConfigDatabase` 支持多表加载、构表、提交前失败保护与版本递增；业务表目录、跨表规则和启动策略属于应用层。阶段 3/P2 的实际 Recipe/Process/Appliance 数据类型是本 change 的输入，当前尚无 cooking 实现或 durable spec。

## Goals / Non-Goals

**Goals:**

- 以 registry 声明 cooking 表及其关系，先构建候选配置，再统一验证后提交。
- 输出可定位到表、记录、字段和关系的稳定诊断，并保持旧有效版本的原子性。
- 生成不依赖枚举/字典顺序的稳定 config identity/hash，供阶段 2 handshake 和未来 Match 启动门使用。
- 验证“增加菜谱/厨具只改数据”的真实可操作 fixture 场景，并覆盖成功/失败矩阵。

**Non-Goals:**

- 不决定 Luban/JSON 等具体工具格式，不复制生成文件，不建立通用 recipe editor。
- 不大量添加菜谱或厨具内容，不实现版本迁移、旧快照迁移、长期存档。
- 不取代阶段 3 recipe loop 的运行时行为，也不将 P1 transport/D4 的 owner 决策写成新的默认值。

## Decisions

### 1. 采用“构建候选—跨表验证—原子提交”流程

先利用通用配置内核构建所有候选表，再执行 ID、外键、能力、拓扑和 Level 引用验证，全部通过才替换当前版本。替代方案是逐表加载，容易让半套配置可见；原子批次与现有配置系统的提交前保护一致。

### 2. Validator 由应用层拥有关系图与业务规则

Config registry 只声明表元数据与读取方式；cooking validator 负责关系图、能力集合、拓扑和产品数据约束。未知能力、断裂拓扑和循环均为错误，不用默认值补全。替代方案是把业务校验塞入通用 ConfigDatabase，会污染其他应用边界。

### 3. Hash 采用规范化输入并区分 Definition/Instance

对已验证表按稳定表名、记录 ID、字段规范化顺序计算 config identity；不把运行时 InstanceId、订单进度或 Match 状态混入。具体算法/编码可实施时选择，但可观察要求是同输入同 hash、任意加载顺序不变、任一定义数据改变可检测。替代方案是文件字节 hash，会受格式化和加载顺序影响。

### 4. 兼容门分层

纯 C# config test 可不依赖 transport；host/client gameplay binding 的 hash 判断仅接入阶段 2 已批准 handshake seam。P1 的 D1-D4 与 LAN 集成仍是阶段顺序出口，不能因 P3 可单机验证而删除。旧版本迁移未获 owner 决策时只允许拒绝/blocked，不自动迁移。

### 5. Test Matrix 与证据计划

| ID | 输入/动作 | 断言 | Runner/产物（future） |
|---|---|---|---|
| C01 | 合法最小表集加载 | 全表原子提交，查询与 hash 可重复 | .NET config contract；normalized manifest/hash |
| C02 | 缺外键、重复 ID、能力不匹配 | 精确诊断，旧库不变，不启动 | .NET validator；diagnostic JSON + before/after hash |
| C03 | 循环/断裂 Process 拓扑 | 拒绝并指出关系路径 | .NET validator；graph error artifact |
| C04 | 新增 Recipe 仅改数据 | 同一 runner 完成校验并可进入阶段 3 loop | parameterized future fixture；两份数据 manifest、loop output |
| C05 | 新增 Appliance 仅改数据 | 既有能力路径通过，无规则代码变更 | 同上；appliance manifest + capability result |
| C06 | host/client 同 hash/不一致 | 一致可继续，不一致在 binding 前阻断 | future handshake integration；compatibility report |
| C07 | 旧 config/快照版本 | 无批准迁移时 blocked/rejected，无静默转换 | future compatibility runner；decision-linked log |
| C08 | 多种表加载顺序同内容 | hash 相同，结果无顺序依赖 | .NET determinism test；hash comparison |

## Risks / Trade-offs

- [Risk] 阶段 3 数据类型尚未落地 → [Mitigation] 任务要求先复核实际类型；不存在时保持 future/blocked，不伪造代码接口。
- [Risk] 规范化遗漏字段导致 hash 碰撞 → [Mitigation] C08 与字段覆盖清单作为证据，算法细节在实施设计审查中确认。
- [Risk] 失败批次污染旧配置 → [Mitigation] C02/C03 断言 before/after 完全一致并复用原子提交边界。
- [Risk] 数据扩展被错误实现为代码扩展 → [Mitigation] C04/C05 是阶段核心出口，失败则不收口 P3。

## Migration Plan

无既有 cooking 配置迁移。实施顺序：复核阶段 3 数据契约→registry/候选构建→validator 与诊断→规范化 hash→C01-C08→阶段 2 handshake 接入（仅有其证据时）。失败回滚删除新增应用层配置与校验代码/fixture，不改通用 ConfigDatabase、旧 changes、路线或主 specs。

## Open Questions

旧配置/快照迁移范围、配置工具格式与 owner 决策沿 [ADR/long-term-goals.md](../../../ADR/long-term-goals.md) 和 [delivery-plan.md](../../../Docs/design/CookingGame/delivery-plan.md) 指针处理；未确认前按 spec 标记 Draft / Blocked，不在此重复定义。
