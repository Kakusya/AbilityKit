# Orca worker：菜单 S04 与 S09–S13

用户已批准总 plan 并要求实施/worktree并行/验证合并 master，Unity后置。你不是唯一开发者，不撤销别人改动；核心 S01–S03 由另一工作树单 owner 修改，网络另有 worker。你只在自己 Orca worktree 工作，不能 merge master/push，也不再创建并行子 agent。

## Target / Ownership

负责 Task S04 的**完整菜单候选解析、来源拓扑与生产映射能力**，随后核心兼容契约已发布后 S09–S13 五批87菜真实内容导入。当前先 S04，不因早期依赖未合入就无所作为。允许新增 src/AbilityKit.Game.Cooking/CookingMenuCatalog.cs / Content/menu-*.json、相应 generator（确定性生成派生内容，记录来源）、CookingMenuCatalogTests.cs / fixtures、新菜单 spec、自己 S04/S09–S13 task artifacts。**禁止修改** CookingRecipeLoop/Checkpoint/Domain/ContentCatalog/ConfigurationValidation 核心已有文件，若生产接入需要其中少量 hook，先向协调者提供精确 patch/interface 请求，由核心 owner或协调者集成，不假装孤立目录已完成实际菜单。

## Change

读取 Docs/design/CookingGame/reference/menu-v0.1 的原始 Markdown/Excel、menu-integration.md、总plan与task register，检查全部87菜、60准备状态、72供应和19功能工位、逐菜节点、来源闭合、重复游戏份数、独立并行分支、must-last收尾。不要相信原文“闭合”声明；实际检查两份文档与所有节点，发现矛盾用具体表/编号记录、按已获助手委托的简单游戏规则解决并写决定，保留原始副本不改。只读 Excel 可用 Python zip/XML，无需新 author xlsx。

建立与 existing CookingContentDocument/v2 runtime 可衔接的强类型图与 validation/mapping，每种供货/准备/成品/process/capability/container/order有明确ID；设计编号与 runtime ID 分离。供应番茄酱≠现场番茄热酱、速溶奶茶≠现泡、黑糖鲜奶无茶。保留汤/吐司旧ID和回归。不能给每菜重写专用拿放。按每步 recipe 的输出物件链接后续recipe，批量饭/茶/酱等引用核心 upcoming YieldPortions/ServePortion，阶段顺序引用明确半成品，不让最终无序配方跳过奶盖最后加。单机贴单策略由S05实现，菜单标饮品需要bind、餐食不需bind。

S04 canonical/hash与校验必须接入实际正式内容加载或明确提供协调集成patch，不能只有文档/死代码。未知数值采用显式 fixture defaults并标测试值，不声称正式平衡；87为目录不是关卡菜单，关卡capability/供应依赖闭合可检测。不新增经济惩罚、精细小游戏/冷却巡检。

## Before coding / Acceptance

读相关Task、spec index/recipe/config、参考README与架构；完善自身PRD/design/implement/manifests（API/错误矩阵/正反例/版本迁移），使用已批准范围进入实施。按 Git identity Kakusya 初始化本机身份如缺，启动Task。实际测试：87唯一映射完整、72/60/19与节点引用闭合、图无环、×2不丢、可独立并行、must-last、缺料/工位/容器阻断，源文档差异可追溯。随后内容每批完整供应→准备→成品→正确容器→交付→恢复，所有87由真实RecipeSimulation/ET host路径可制作而非只数JSON行。

核心schema/checkpoint有唯一owner，将升级新执行模式/份数与空间；不要自己再改schema。首次先完成S04实际源审计与映射，实现可测试production catalog并提交。向协调者报告接口需求/冲突/branch/path/commit/真实测试与剩余内容。若依赖阻塞，Orca ask/mail请求最新core commit，持续完成独立验证，不把未接入算完成。按Orca preamble完成生命周期。
