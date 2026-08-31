# Triggering 兼容记录

该文件仅保留历史边界记录。正式主线请使用 `Runtime/Plans`、`ActionScheduler`、`RuleScheduler` 与相关验证/诊断入口。

当前 Runtime 根目录已不再保留 .cs 兼容占位入口。

- 已清理的旧主线运行壳包括 `Runtime/Dispatcher`、`Runtime/Scheduler`、`Runtime/Executable` 与 `Runtime/Legacy`。
- 当前不应新增任何旧兼容入口；历史资料仅允许以只读说明或迁移备注存在。
- 相关正式边界请参考 `Document/FormalApiBoundary.md` 与 `Document/LegacyMigrationPolicy.md`。
