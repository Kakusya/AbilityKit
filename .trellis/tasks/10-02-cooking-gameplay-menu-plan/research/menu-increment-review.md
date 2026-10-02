# 菜单来源映射增量独立审阅

日期：2026-10-02。审阅对象：`cooking-menu-s04` 的提交 `3b2797789e9dd6c26d55821ad7a45a3389e00971`。审阅者只读保留分支；worker 仍继续工作，未编辑其代码、任务状态或运行构建门禁。本记录写在主工作区。

## 结论

该提交可以作为**来源身份、审计和配置图映射增量** cherry-pick 到 integration，未发现阻止这一受限用途的代码问题。它不是 S04 完整退出、S09–S13 完成、87 款制作交付完成或 master 产品验收。接收后仍须在 integration 的实际组合版本复跑适用门禁，保留共享 csproj 的其他增量。

## 独立核对证据

- 重新只读调用 `source_audit()` 和 `project()`，生成内存对象与提交中的 `menu-catalog-v1.json`、`research/source-audit.json`逐字段一致：87 菜品、60 准备件、72 供应原料、19 工位、210 展开配方、367 来源节点。没有将生成结果写回文件。
- 实际附件 SHA-256 与目录 README、catalog Sources 和审计对象一致：Markdown `8d503eeaaf5c2de3e326762beb9b1bf305342b1095b0ba9fcabcde230f902cc2`；Excel `5268130330044901cfaecf3cbd7ab048432030ef03c6f17cf3a793d3d744232d`。
- `tools/cooking-menu-source-ids.json` 用来源 ID 和预期名称绑定 slug；生成器拒绝缺失、未知、重复 ID、错名与重复 slug。物料/工位/菜品投影按 ID 排序，来源行号仅用于物理追溯，不赋予身份。
- 独立执行 `python -B tools/test_cooking_menu_generator.py`：5/5 通过。其重排测试反转各表，比较完整语义图及去除 ExcelRow 后的来源节点；另验证 367 原单元格处分、来源 ID/名称拒绝、31 个保留差异和模具阶段链。使用 `-B`，不写 Python 缓存。
- 367 个节点保留十列 SourceCells、来源 ID/节点组合与 ExcelRow；SERVE 独立为交付操作，其余节点关联具体配方。生成器还递归展开 Stage，逐项证明与来源直接投入的份数多重集相同。31 个差异均为饮品工位闭包新增贴单依赖，未改原附件。
- 检查 `research/gate-source-increment/gate-summary.json`、实际原路径日志和 TRX：两次构建 exit0，日志报告零警告/错误；厨房聚焦 128/128、Cooking 回归 249/249、ET 回归 67/67，TRX 实际存在且失败/跳过为零。Cooking TRX 包含该提交全部 23 个 catalog 用例；ET TRX 没有菜单专属用例，不能由其 67 例推导菜单 ET 闭环。
- `focused-tests.log`报告 23/23。审阅检查了具体用例语义：份数投影、分支独立、末步前置、错误引用/循环、来源追溯、可供范围、身份与防御复制等。只有 F31 覆盖实际 formal loader 加载，尚未通过公开命令完整制作交付。

## 未修复项及完整接入门

以下均属已登记的跨 owner 接入工作，未在本次只读审阅中修改：

1. `MapLegacyRecipe`明确拒绝 Manual、批量与重复投入；完整生产 adapter 仍等待 core。自定义 recipeFactory 当前校验基础配方和多重集，无法证明新 Execution/YieldPortions 字段与载体约束已安装。23 例里的 ProjectionShape 是形状测试，不是这些运行规则的证明。
2. `RequiredProcessingContainerDefinition`尚未接入既有加工 authority；carrier 目前参与图闭包和容量兼容检查，不能阻止玩家运行时用错误容器加工。模具/烤盘等流程顺序目前仅由 Stage 图表达。
3. 菜品 RequiresBinding 和容器 Disposable 当前只存在 catalog/来源投影；饮品提交强制核票、杯子提交后不入洗池及补杯流程仍待 S05/S07 的真实规则接入。
4. Catalog/source SHA 与所选菜单尚未进入正式 configuration/checkpoint identity。catalog Sha256 改变的测试不证明生产 content.Identity 或恢复身份同步改变。
5. 原料 8 份、共享批次产量 2、容器 1/2 个、tick1、score100 均为显式 fixture。常规初始供应当前可能堆在同一 world location，不能代替单槽空间和有限采购规则的正式准备流程。
6. 缺少全部 87 款通过公开命令加工/暂存/盛装/核票/交付、错误次序/错容器反例及 ET 固定 Tick/恢复继续等范围相称证据。`ValidateLevel`检查集合闭包，不证明物理可达或实际 Ready 阶段接入。

## 验证边界与后续变更

审阅未新跑 .NET 编译/测试门禁，避免与 live worker 争用其输出；TypeCheck/构建结果以上述实际日志为既有证据，不冒充本审阅复跑。源码变更没有独立 lint 配置执行，本次 lint 不作通过声明。

审阅期间 worker 开始新增 `Compatible_automatic_route_uses_real_public_commands_from_raw_supply_to_settlement` 用例和 runner 设计记录，尚未属于受审提交。不要把这些未提交变更或后续运行结果回填为 `3b2797789…` 已有证据；待新提交再核对。

## 追加审阅：准备件输出存放容器

对象：`bb167ddf84f40db725a15dc2cc4810dcdfd83f42` 相对 `a14d4a178` 的增量。结论：**可接收为来源目录与依赖闭包补全**；未发现阻止该增量接入 integration 的问题，不据此批准批量运行或 S04 完整退出。

- 新 `CookingMenuStep.OutputStorageContainer` 是可空、默认 null 的目录字段。生成器对全部 60 个准备件的最后一步，取来源半成品表“加工/存放容器”列最后一个斜杠分项作为存放选择；保留原文于源审计，不改变加工 Carrier，也不替换加工 authority。
- 只读内存重算生成目录与已提交 JSON 一致；逐个核对全部 60 个准备件：字段名称匹配该来源末分项、容器存在、接受实际 output、容量不少于 fixture YieldPortions。独立 `python -B tools/test_cooking_menu_generator.py` 再次 5/5 通过，包含来源行重排与原节点追溯。
- Requirements 递归访问步骤时将可选输出存放容器加入闭包；ToContentDocument 原有 required-container 投影因此可注册其容器定义及初始 fixture 空容器。Validate 对不存在的存放容器报告 MissingContainer，对不接收 output 或容量不足报告 InvalidOutputStorage。这是可供集合和形状校验，不是空间可达、装满后的恢复或按份取用行为。
- 新四例 Theory 分别检查 P19 饭桶/F08、P20 备料盆/F21、P35 酱料盆/F21、P53 小料罐/D25 的正例与删除实际 output 兼容性的负例。不存在存放容器与容量不足两条拒绝分支由代码检查确认，当前新增用例没有分别执行这两条分支；不得声称覆盖所有拒绝路径。
- 独立读取实际 `local/Logs/test-gates/20261002-184302-cooking-kitchen-loop/cooking-kitchen-loop/gate-summary.json` 与原路径日志/TRX：两构建 Passed/exit0；聚焦 133/133、Cooking 254/254、ET 67/67，失败/跳过均零。四个新增 storage 正反例在 Cooking TRX 均 Passed。`storage-tests.log`记录 catalog 28/28，这是 worker 已构建后 --no-build 运行，不冒充新的编译。本审阅未新跑 .NET。

尚缺的运行证据保持原结论：ServePortion 是否扣减批量余额、将份转入不同容器、存放容器满时零变更、搬运保存份数和物品唯一位置、继续加工/公开交付/恢复等均未由 storage 元数据证明。来源列的斜杠是加工与暂存候选表达；选定末分项供闭包登记，不意味着运行时必须经过这个容器或已有强制迁移规则。实际运行接入仍需 core/S07 等 owner 的规则与测试。批次 fixture 容量和产量不是正式平衡值。

本次工作树检查为 clean，审阅没有修改 live worker 文件。Lint 未新执行；TypeCheck 仅核对上述实际构建证据；代码与任务 metadata 未改。
