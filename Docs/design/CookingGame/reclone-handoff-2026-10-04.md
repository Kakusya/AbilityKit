# 2026-10-04 仓库重建交接

本文补充 [会话收尾记录](session-closeout-2026-10-04.md)，不改变原有验收、未完成或延期结论。

## 仓库与目录

- 远端是 `https://github.com/Kakusya/AbilityKit.git`。GitHub API 确认它仍是 `HOBOBO/AbilityKit` 的 fork；Orca 显示父仓库名称不表示 Git origin 指向父仓库。
- 新克隆为 `D:/MyWork/AbilityKit-fresh`，初始基线 `a2cd7284e12d50a10bbb7abee6fc265577b9aa7c`；本地只有 master，跟踪 origin/master。新 Orca repo ID 为 `aadd13f6-2b33-42f1-89c6-e304634149b0`，仓库身份为 `github.com/Kakusya/AbilityKit`。
- 误上传的十二个历史/草稿分支已删除，远端仅保留 master。没有将未验证草稿合入主线。
- 旧 Orca host setup 已移除。旧目录 `D:/MyWork/AbilityKit` 尚未删除：当前会话占用导致整体移动失败，清空命令被自动安全审查拒绝。清理前必须重新检查会话、进程和 Orca 准备中的 worktree。

## 本机证据位置

备份根目录为 `D:/MyWork/AbilityKit-reclone-backup-20261004`。该目录不随 Git clone 分发，交接到其他机器时须另行传递所需归档；缺少原始材料应记录证据不可用，不能据摘要重新宣布验证通过。

| 相对备份根目录的路径 | 保存内容 |
| --- | --- |
| all-refs.bundle | 重建前全部引用和完整历史，实际 git bundle verify 通过，包含十二个历史/草稿分支 |
| refs-before.txt | 原引用与精确提交 SHA |
| local/ | 原 local 证据副本；26,349 个文件、3,563,226,412 字节，逐文件 SHA-256 与源文件一致 |
| backup-verification.json | 本次副本数量、字节数与校验摘要 |
| trellis-local/ | 本机 developer、runtime、Kakusya journal 备份，不作为共享项目配置提交 |

旧报告的 local/... 证据现在可从备份根目录下的同名路径读取。先用 `git bundle verify <bundle路径>` 检查，再用 `git clone <bundle路径> <独立恢复目录>` 恢复历史；恢复不表示批准合入 master。

## 本地说明文件的交接要点

### 物理 LAN 准备包

`local/Artifacts/cooking-network-verified-b6-20261003/README.md` 对应历史 b6e102ac9 构建；33 个文件经过 SHA-256 核对，旧 verified 报告只证明同机独立进程结果，不证明物理 LAN。包需要兼容的 .NET 10 runtime；run-host.ps1 / run-client.ps1 属于当时构建，不是最新源码验证。

实际验收仍须读取 [physical-lan-runbook.md](../../../.trellis/tasks/10-02-cooking-network-gameplay-loop/research/physical-lan-runbook.md)，保留新两端输出、退出码、实际机器和网络地址、报告，核对完整经营与重绑、hash、scope、instance、MVID，并完成争抢与拒绝控制。物理两 PC 仍不可用、NOT_VERIFIED。

### 性能候选归档

`local/Artifacts/network-cost-candidates-20261003-574c182e0/README.md` 记录隔离 O01/O01b/O02：当时归档 977 个文件、142,726,248 字节，逐文件核对且完整历史 bundle 验证通过。四组 reference 配置全部 NOT_ACCEPTED，没有生产采用或主线导入。

部分早期 O02 focused DLL 已被后续构建覆盖，明确 UNAVAILABLE；不能用后来 Debug 二进制冒充原始 focused 构建。详细依据为该归档的 manifest、hash verification、binary availability 和四份 v4 evaluation。

### 历史设计草稿

`local/Artifacts/worktree-retirement-cooking-integration-s06-s14-20261003/` 保存 preparation、menu policy、natural operating、technical recovery 的历史文档。它们不能覆盖当前 spec、task/check 或验收结论；采用差异需按当前源码重新审查。

## 继续工作的边界

S01–S14 / N01 已验收；N02/N03 整体仍未完成。优先使用现有框架解决功能、合作、恢复和故障正确性。普通延迟/吞吐、native timer/scheduling、O04 集成仍 OWNER_DEFERRED；Unity/S15 继续后置。

接续入口为 [progress.md](progress.md)、[functional-first-owner-disposition.md](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/functional-first-owner-disposition.md)、[current-network-exit-refresh.md](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/current-network-exit-refresh.md)。本次没有运行新的构建或测试，不新增验收声明。
