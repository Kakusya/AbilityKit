# 做菜规划迁移来源

本目录是从已移除的旧工作流导出的只读来源快照。它保留七份尚未实施的做菜规划的原始文件与 SHA-256 清单，服务于可追溯性；它不是活动 task、spec 或验收状态来源。

活动入口：

- `.trellis/spec/cooking/index.md`：阶段规范索引。
- `.trellis/tasks/09-15-cooking-*/`：待审阅的 PRD、design、implement checklist、context manifests 与研究来源。
- `manifest.json`：每个来源文件的路径、哈希、阶段、依赖、阻塞项、规划验证标识与目标映射。

所有迁移 task 保持 `planning`。只有实际执行并记录检查证据后，才可进入 Trellis 的后续生命周期。
