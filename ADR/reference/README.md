# 参考资料与安装记录

更新：2026-09-14。参考源码在被 Git 忽略的 `upstream/`，实际技能位于宿主发现目录，二者不混用。

## 实际安装

- **CodeStable v2**：由 `npx --yes skills add codestable/CodeStable/plugins/codestable --skill '*' --agent codex -y` 安装到 `.agents/skills/`。本项目保留 7 个入口（含 `cs-code-review` 兼容入口），已移除 `cs-feat` 与 `cs-epic`；来源及内容哈希见根 `skills-lock.json`。
- **OpenSpec 1.13.0**：本地 npm 依赖在 `tools/openspec-cli/`，版本及依赖树固定于 package-lock。运行官方 `init --tools zcode --profile core --language zh-CN`，生成的 6 个技能现已迁入 `.agents/skills/`和 `.zcode/commands/opsx/` 的 6 个命令，以及 `openspec/config.yaml`。
- **grilling**：`.agents/skills/grilling/SKILL.md`，来源 `mattpocock/skills`，内容哈希见根锁文件。
- **本地 CLI**：根目录执行 `powershell -ExecutionPolicy Bypass -File tools/openspec-cli/openspec.ps1 <参数>`；包装入口关闭遥测并恢复调用前的环境变量。技能模板中的裸 `openspec` 使用此入口替代。其他机器先执行 `npm ci --prefix tools/openspec-cli`。无全局安装。
- 两个既有技能工程工具 `build-cs-skill`、`eval-cs-skill` 保留原样，不算 v2 运行时入口。

## 备份与升级边界

升级前完整备份 `.agents/skills/`、`skills-lock.json`、`AGENTS.md` 并记录 SHA-256，位置：`upstream/backups/20260914-143537/`。官方列出的 24 个退役入口已移至该备份的 `retired/`，不再被技能目录扫描；旧内容没有销毁。恢复时应先核对备份清单及当前改动，不直接覆盖整个工作区。

CodeStable 项目骨架仅为 `.codestable/attention.md`、`lessons/`、`work/`，没有安装退役的 v1 runtime。OpenSpec specs/changes 暂为空，没有虚构已实现功能或批准游戏实施计划。

## 参考来源

| 资料 | 来源与参考提交 |
|---|---|
| OpenSpec 源码 | https://github.com/Fission-AI/OpenSpec — `9d4e5974e5c0d9a09b9c6c1e1eb0975e80ec4461` |
| CodeStable 源码 | https://github.com/codestable/CodeStable — `19c796de5990973a9e5d44aa9536d90513e37b5c` |
| grilling | https://github.com/mattpocock/skills ，`skills/productivity/grilling` |
| 用户分享 | https://chatgpt.com/share/6aa78930-7c50-83ee-9185-280d67d139ec |

分享 URL 之前连接超时，但用户已在后续消息粘贴正文；其统一入口、双向读取、唯一事实源和 attention 指针要求已落实到根 AGENTS、OpenSpec config 与 CodeStable attention。所引旧版 OpenSpec 文件布局不直接照搬，以实际 1.13.0 初始化结果为准。

## 验证边界

本地 CLI 版本和空变更查询成功；技能列表同时列出 CodeStable、grilling 与 OpenSpec 的实际安装路径。`validate --all` 返回没有可验证项目，不是产品测试通过。当前会话的宿主工具列表是否已热刷新没有证据，必要时新开会话加载新入口。参考和备份不属于激活技能。

## 本项目裁剪与更新注意

2026-09-14 按用户确认移除 cs-feat/cs-epic，修改 cs 及 issue/refactor/review/onboard 的维护路由。完整修改前备份：`upstream/backups/role-split-20260914-145810/`。这些入口已是本项目适配版本，根 skills-lock 的上游哈希是安装基线，不代表本地适配后的内容哈希。

今后不要无差别执行 CodeStable 全包覆盖更新，否则会恢复功能入口并覆盖路由。应先备份、比较上游变化、仅更新保留入口并保留项目分工。OpenSpec 更新优先使用 agents 适配，技能唯一安装在 `.agents/skills/`；不重新产生 `.zcode/skills/` 同名副本。ZCode 专用命令可从官方生成结果定向更新。
