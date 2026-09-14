# OpenSpec × CodeStable：统一知识读取与归属

状态：按用户提供的桥接要求落地。入口是根 `AGENTS.md`，不是任一技能自行决定的私有上下文。

## 读取桥

无论从 OpenSpec 还是 CodeStable 开始，规划或修改代码前均读取 `.codestable/attention.md`，按任务关键词检索并读取相关 lessons，再读取相关 specs 和当前 change 的 proposal、差量 specs、design（若存在）、tasks。架构工作加读相关 ADR 与既有 `Docs/design/`。没有活动 change 时不猜测；没有行为规格时区分长期目标与已实现能力。

根 AGENTS 对所有工作流规定此顺序；`openspec/config.yaml` 的 context 同时携带这些读取要求，使 OpenSpec 产物指令也能获得桥接；attention 反向指向 OpenSpec 的唯一归属。规则可要求读取，但不是文件权限强制机制，不能宣称任何 Agent 都必然遵守。

## 单一事实源

| 信息 | 唯一归属 |
|---|---|
| 长期方向与尚未明确的产品范围 | `ADR/long-term-goals.md` |
| 已确认架构取舍 | `ADR/decisions/` |
| 行为需求与验收契约 | `openspec/specs/` |
| 当前修改原因、差量、设计与实施清单 | `openspec/changes/<name>/` |
| 未被正式文档或机械约束承接的工程经验 | `.codestable/lessons/` |
| 极少量跨会话提醒与读取指针 | `.codestable/attention.md` |
| 必要的跨会话恢复状态、证据指针 | `.codestable/work/` |
| AbilityKit 框架设计 | 既有 `Docs/design/` |

不要在 attention 复制数值需求，不在 lessons 写另一份产品契约，不在 work 复制 tasks。稳定教训进入测试/checker 或正式文档后，旧 lesson 改为指针或退役。来源冲突必须指出，不静默挑选。

## 最小闭环

1. grilling 调查事实并澄清产品选择；建议不视为批准，当前目标见长期目标文档。
2. OpenSpec 维护需求与具体场景，设计仅在有真实取舍时增加；ADR 保存长期结构性决策。
3. 功能开发全程由 OpenSpec 推进，不交给 CodeStable 实现。CodeStable 仅负责既有契约下的 bug、等价重构、文档维护、审查和经验；它读取相关 change，但不复制任务。维护中发现预期行为需要改变时交回 OpenSpec；期望不清时先澄清。
4. 以实际验证结果验收，不将未执行测试写成通过。验收后归档变更并同步生效规格；同步不证明功能已实现。
5. 普通任务不额外生成阶段档案；跨会话才按需建立一个 work 游标。

## 工具边界

CodeStable、OpenSpec、grilling 技能统一安装于 `.agents/skills/`；ZCode 专用命令保留在 `.zcode/commands/`。CodeStable 的功能开发与 Epic 入口已移除，保留入口采用本项目维护路由。外部参考与旧技能备份位于被忽略的 `ADR/reference/upstream/`，不会自动加载。未复制 v1 的 runtime/reference/gates 骨架。具体安装和版本见本目录 README。

文档整理在原有 canonical 文档中完成。产品契约变化不是普通文案修改，必须交给 OpenSpec；提案 ready、tasks 完成、archive 和实际验收通过不是同一状态，不互相替代。
