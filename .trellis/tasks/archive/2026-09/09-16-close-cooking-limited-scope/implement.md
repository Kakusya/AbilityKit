# Implementation Plan

## 1. Governance records

- [x] 1.1 新建统一 prohibited Unity future scope。
- [x] 1.2 新建 non-Unity successor backlog。

## 2. Re-scope legacy tasks

- [x] 2.1 P0–P6 task metadata 改为 completed-limited-scope 语义。
- [x] 2.2 PRD/design/implement 将 Unity 执行项移出当前完成条件。
- [x] 2.3 P1–P6 非 Unity 未完成工作指向 successor backlog。
- [x] 2.4 七个 check.jsonl 追加 closure，保留历史事件。
- [x] 2.5 修复 P0 manifest artifact context 与 P1 test-count drift。
- [x] 2.6 独立审查修复：清除 Unity blocker/前置语义；P3-P6 历史 checklist 标为只读不可执行；task metadata 按 archive/successor/future 分流。

## 3. Shared documentation

- [x] 3.1 更新 Cooking progress 和 technical roadmap。
- [x] 3.2 更新 cooking spec index 和七份阶段 spec 状态/宿主表述。
- [x] 3.3 更新交付计划或其他直接引用旧 active 状态的入口。
- [x] 3.4 预改外部 task/check 链接与跨 task manifest 引用到 `archive/2026-09/`；保留 task 自引用供 Trellis archive remap。

## 4. Archive and validation

- [x] 4.1 已使用 `task.py archive --no-commit --skip-branch-validation` 归档七个旧 task；七条命令均成功，归档副本 status 均为 `completed`，未自动提交。
- [x] 4.2 已在归档前验证治理 task 与七个旧 task manifests；独立审查修复后八项均通过，六个旧 task 仅警告已删除的历史 branch。
- [x] 4.3 已搜索残留状态/路径冲突并运行 `git diff --check`；真正外部链接已预改 archive 路径，仅保留 Trellis 可 remap 的 task 自引用；diff check 通过。
- [x] 4.4 已记录归档后检查结果：七个 archived task 与治理 task validation 均通过；六个 archived task 仅有历史 branch 已删除警告；`task.py list --json` 只列出本治理 task；`task.py list-archive 2026-09` 列出七个归档 task；stale status/Unity successor-blocker 冲突搜索为 0；`git diff --check` 通过。代码构建、测试、Unity 检查与运行时 gate 未运行，因为本次仅修改文档与 Trellis task 治理，没有运行时代码变化。
