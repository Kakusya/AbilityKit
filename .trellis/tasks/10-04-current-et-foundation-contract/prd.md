# Issue #5 当前 ET 架构合同与 AGENTS

## 目标与授权

用户要求重新读取 Issue #5 并执行直到目标。最新 Issue 已批准文档与源码核验范围，2026-10-04 #5 唯一 orca-ready；#1–#4/#6–#9 blocked。以 research/issue-5-snapshot.md 与 metadata 为本次读取快照，提交审阅前重新核对。

## 基线

HEAD 5312c6e4bf2b612260297e2d8623aa362a9051e6，分支 Kakusya/p0-et-agents.md；初始工作树干净，origin Kakusya/AbilityKit。相比 a2cd7284 仅新增 48 行 reclone handoff。源码审查不是构建或运行验收。

## 范围与验收

A：实际/目标树和十个状态族的类型、writer、生命周期、索引、一致性组、旧入口退役和正负控制。
B：Command、准入/终态、Domain Event、Projection/Baseline、Session control 五类合同，以及普通操作和断线重试的实际代码时序。
C：命令提交后 fixed-step 失败保留 effects/event/Executed terminal；LogicalTick/HostFrameSequence 不推进。三个时钟、单 owner、幂等与旧代次保护。
D：实际框架/协议能力与版本、来源、许可、宿主闭包、消费者和退役条件；区分 JSON Wire3、MemoryPack catalog、ET proto 与可选 protobuf exporter。
E：精简 AGENTS，唯一当前进度入口，原通知与问号损坏逐字留档；规则对应现有/待建 gate 或人工 check。

验收需具体文件/符号证据，现状/目标分离，不修改运行时/依赖/协议/CI，不升级、不提升 ADR 状态、不解锁 blocked issue；链接、UTF-8、Markdown、diff 实际检查留日志。.NET/Unity NotRun。

## 边界与决策

只修改 AGENTS、progress、相关架构/参考文档及本 task 计划/检查材料。不清理用户目录、备份或进程，不合并/部署。Issue 回报仅公开脱敏仓库证据。运行时迁移与新机器门禁留给独立批准任务。
无待决产品选择；保留当前行为，目标命名均为提案。若发现必须改变事务或产品边界才可继续，提交具体差异等审阅。
