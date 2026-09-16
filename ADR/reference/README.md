# 参考资料与 Trellis 安装记录

更新：2026-09-15。Trellis 是本仓库唯一的活动 AI 工程工作流；项目共享配置由 Git 跟踪，开发者身份、runtime session 和本机缓存遵循 `.trellis/.gitignore`。旧工具镜像、备份与无关的 skill 均已清理。

## 实际安装

- **Trellis 0.6.17**：官方包 `@mindfoldhq/trellis@0.6.17`，来源 <https://github.com/mindfold-ai/Trellis>。已使用 `npm install -g @mindfoldhq/trellis@0.6.17` 安装，并在仓库根目录执行 `trellis init --zcode --codex --user "Li He" --yes --no-monorepo` 初始化。
- **前置条件**：Node.js `>=18`、Python `>=3.9`。本次初始化实际检测到 Node `v24.18.0` 与 Python `3.12.10`。
- **项目共享内容**：`.trellis/config.yaml`、`workflow.md`、`spec/`、`tasks/`、`scripts/`、Trellis 生成的 `.agents/skills/trellis-*`、`.zcode/` 与 `.codex/` 集成文件。
- **本地运行态**：`.trellis/.developer`、`.trellis/.runtime/`、Python cache、局部 agent/session 文件等由 `.trellis/.gitignore` 排除；不要宽泛忽略整个 `.trellis/`。
- **升级**：先检查 Git 状态并审阅自定义的 `.trellis/workflow.md`、`config.yaml`、spec 和 task；CLI 使用 `trellis upgrade`，项目模板使用 `trellis update`，需要模板迁移时使用 `trellis update --migrate`。升级后审阅 diff 和重新验证 hooks/任务 context，不能假设生成内容保持不变。

## 做菜规划迁移

七份做菜规划曾从旧流程迁移，并于 2026-09-16 经 owner 按已有 evidence 重划为纯 .NET `completed-limited-scope` 交付：

- 规范与状态索引：[`.trellis/spec/cooking/index.md`](../../.trellis/spec/cooking/index.md)。
- 七个历史任务按已有 `check.jsonl` 支持的 pure .NET limited delivery 归档；完整产品、真实 LAN、durable storage 均未因此完成。非 Unity 后续见 `Docs/design/CookingGame/successor-backlog.md`。
- Cooking Unity 执行长期禁止；历史条目与重新授权条件见 `Docs/design/CookingGame/future-scope.md`。
- 只读来源快照与 SHA-256 清单：[`.trellis/migration/legacy-cooking-changes/README.md`](../../.trellis/migration/legacy-cooking-changes/README.md)。快照用于追溯，不是活动任务或验收状态来源。

迁移本身不代表实施或验证；后续代码与归档状态来自各 task 的实际 check evidence 和 2026-09-16 owner closure。若 successor 获批，必须新建 task 重新审议；不得恢复旧 task 或从归档推导完整阶段已完成。

## 宿主集成与验证边界

- ZCode hooks 由 `.zcode/config.json` 配置；如果宿主禁用了项目 hooks，需按 Trellis 初始化输出提示安装 bridge 并新开会话。磁盘文件存在不等于宿主已加载或 hook 已获授权。
- Codex hooks 需要在用户级 `~/.codex/config.toml` 启用 `features.hooks = true`，并在支持的版本中完成 `/hooks` 批准；未启用时仍可手动读取 `.trellis/` task/spec，但自动注入不可视为已生效。
- Trellis check 编排检查，不替代项目测试结果。实际命令、通过、失败、受阻和跳过原因必须如实记录；Unity 环境不全或另一 Editor 占用项目不算通过。
- 当前会话的宿主技能列表不会因磁盘变更自动热刷新；需要新开会话加载新入口。

## 清理边界

旧的 OpenSpec、CodeStable、grilling、历史技能镜像和备份均已从工作区清理。仍保留的历史规划只有 [`.trellis/migration/legacy-cooking-changes/`](../../.trellis/migration/legacy-cooking-changes/)，其中保存七个做菜 change 的只读快照与哈希清单，供 Trellis 活动 tasks 追溯；它不包含旧工具或活动 skill。
