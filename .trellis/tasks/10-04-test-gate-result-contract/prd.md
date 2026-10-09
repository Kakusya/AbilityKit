# Issue #6：门禁结果真实性与覆盖合同

状态：planning / 等待 Dot 审议。Owner 已要求 #5 收尾后直接开始 #6；开始本计划不跳过 Issue 正文要求的计划/schema 审议门。

## 依据与目标

[最新 Issue 原文](research/issue-6-requirements.md)、[源码清单](research/source-inventory.json)、[实施方案](design.md)、[执行与验收顺序](implement.md)。实际干净 base 是 `7aa3e8b67c13a469192edaa761cb3dfc1fa9294b`；#5 最终合同 `530609fd0` 已获 Dot 接受，PR #10 合并后已收尾。采用 AGENTS 的五状态、声明覆盖、零测试限制与 provenance 合同；更早 proposal 中相冲突的措辞不作为当前规则。

已读 #5 全文体检报告、AGENTS 全文提案及实际合并 AGENTS/合同 E；读取 progress、reclone handoff、AbilityKit spec/validation、测试规范与共享跨层/复用指南。旧 local 原件缺失的历史验证不重建为通过。本轮只读源码并提交文档/schema 示例，未修改或运行生产门禁。

现状：Unity mirror 缺 DLL 目录时 exit 0，父脚本将 exit 0 直接映射 Passed；同时缺少完整覆盖、足够运行身份和所有未执行步骤。目标：按真实结果、声明覆盖、原始证据和产物身份判断一次运行；控制台、父子结果及退出码一致。

## 批准后的交付范围

运行器、Unity mirror 结果、唯一 gate 配置、测试规范、隔离自测；保留已有 TRX/XML 验证。所有 18 个现存脚本入口的结果输出兼容方案见 design。任何扩大到 helper/脚本的改动须由本计划明确审议，不能以只有 fixture 通过替代生产兼容。

不改变 gameplay、ET/SDK/依赖、CI/权限、Unity/S15 或延期性能任务。不可用 Unity 环境不安装/升级；Unity mirror 仅证明对应 asmdef 镜像。

## 完成条件（当前均未完成）

- [ ] Dot 接受具体计划、schema、覆盖政策及兼容路径后才 in_progress。
- [ ] 修复前复现缺 Unity 的误报；修复后同条件不 Passed。
- [ ] Issue 所列正负控制自动执行，输出命令、数目、原件、退出码和 exact SHA。
- [ ] 所有配置 step kind 和运行器支持但尚未配置的 execute-method 分支同合同；18 脚本无 exit0-only 的成功旁路。
- [ ] 一项适用真实 .NET gate；Unity 仅实际可用且获准覆盖，否则 Blocked/Skipped。
- [ ] Dot 复核最终 diff/真实命令/负例；不自动解锁其他 Issue。
