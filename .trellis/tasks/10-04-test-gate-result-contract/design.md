# 小型实施计划与结果合同提案

状态：待 Dot 审议，尚未改运行器。base `7aa3e8b67c13a469192edaa761cb3dfc1fa9294b`，开始前干净；源码和配置相对最初审计基线未因 #5 的文档工作改变。

## 1. 状态、退出码与覆盖

| 状态 | 含义 | 规范 CLI exit |
| --- | --- | --- |
| Passed | 实际完成本结果声明的全部 requiredCoverage，验证和身份均有效 | 0 |
| Failed | 执行/结果验证失败；无效/矛盾报告、超时或运行后取消也在此 | 1 |
| Blocked | 缺必需工具、环境、产物前提等，无法可靠开始/继续 | 2 |
| Skipped | 依声明政策主动不运行的可选步骤；有原因、无已完成覆盖 | 3 |
| NotRun | 聚焦未选、前序中止或未启动即取消；不宣称执行 | 4 |

记录 child 的原始 processExitCode，和父校验后独立 derived cliExitCode；缺安装的 Unity child 自身 Blocked/2，允许缺 Unity 的可选父边缘转换为 Skipped/3，保留原 child 结果与转换原因。父 requiredCoverage 不含该覆盖时可 Passed/0，但该缺失仍在摘要。任何实际失败、协议失败或身份矛盾，即使可选也不吞成成功。

每个 gate/step 在 test-gates.json 声明 requiredCoverage、optionalCoverage 和 step 是否必需。覆盖使用可辨识的项目/过滤器/配置/宿主 token，不用模糊“dotnet 全通过”。所需 token 必须由有效且完整证据的当前结果提供；父递归聚合，禁止用 child exit0 作为覆盖。Failed 优先，其次 required Blocked，其次未完成 required NotRun；均无才 Passed。全部可选且全部未跑，不存在已完成声明覆盖时为 Skipped。未知 gate、配置/合同无效为 Failed。

政策提案：保留 core-stability 既有纯 .NET 与静态检查必需覆盖；其中 Unity mirror 标为 optional 并明确从该 gate 的验收范围排除 Unity 证明。所有明确 Unity EditMode/PlayMode gate 继续 required，不弱化为 optional。无需增加独立 gate 名称；直接 mirror 命令请求 mirror 覆盖时缺环境 Blocked。此政策必须由 Dot 确认，不能仅为退出成功而默认降级现有覆盖。

- `-StepName`：保留所有未选步骤为 NotRun；选择步骤成功单独记录，完整 gate 的 requiredCoverage 缺失则 NotRun/4，fullGateAccepted=false，不打印完整通过。该非零退出是明确兼容变化，待 Dot 确认。
- `-NoBuild`：必须提供匹配 source SHA/dirty 输入清单、配置/TFM、SDK、项目引用/生成器/源码 hash 与实际 binary hash 的 build manifest；缺失 Blocked。dirty 情况只有全输入指纹相同且未发生并发改动才可匹配。不从 DLL 日期推导来源。
- `-NoRestore`：验证 assets/lock/项目及工具版本的 restore 前提身份，记录 restore 未运行；不能声称 restore 通过。任意内嵌 NoBuild（如 smoke 配置）也受同一校验。旧版本没有 manifest 时保守 Blocked，不临时补造旧证据。
- 声明应执行测试时 total/executed/result entries 必须大于零且计数、失败、未执行/跳过与覆盖一致；纯构建/静态审计 tests=null（N/A）可按自身覆盖通过。

## 2. 单一结果来源与身份

入口生成 UUID runId，按调用路径/index 加 UUID 为每次 step/child gate 生成 resultId，复用同名 gate 也不能混目录。创建独占目录失败时不复用；所有结果在运行开始后通过同目录临时文件原子发布。

结果记录 schemaVersion、source SHA/dirty/start/end、工具实际版本、原始子进程退出码、校验后的状态/CLI、具体命令、配置 hash、覆盖、reason、原始 log/报告与产物清单；产物包含 producer run/result ID、相对路径、字节数和 SHA-256。父仅从规范化结果树派生一份 summary，再渲染控制台并选退出码。NotRun 结果无实际开始/结束、processExitCode=null，不捏造命令执行。

JSON Schema 在 [result.schema.json](research/result.schema.json)；示例在 [result-examples.json](research/result-examples.json)，example=true 表明是未运行提案，不能当验证回执。JSON Schema 只检查形状，跨字段身份、coverage 集合、计数、退出码和磁盘 hash 必须由唯一 validator 校验；example=true 的记录不能被生产验收。

公开报告用仓库/run 相对路径、工具符号名和去除敏感值的命令参数；本地原始命令映射在 ignored run 目录中。不遍历环境导出凭证，不向公开 JSON 写用户绝对路径。start/end 校验 SHA/dirty 输入指纹，运行中来源变化则 Failed。hash 证明字节身份，不伪称掉电保证或敌对机器可信签名。

## 3. TRX/XML、二进制与旧结果

保留并加强 Assert-DotNetTrxResult 与 Unity XML 校验。当前 run/step 独占路径中收集刚执行的结果，用父 context/manifest 将 TRX/XML（格式本身未必携带自定义 runId）绑定到本次命令及被加载二进制清单；核验 source/config/producer identity、报告 hash、计数/失败和 binary before/after hash，不用全局 mtime 替代。

禁止从旧目录捞合法 TRX；Unity 日志中的恢复路径只能是本次 run-owned 目录且必须证明同一次调用来源，否则 Failed。后续覆盖的 DLL、不同 SHA 或 producer ID 不接受。对于新鲜非空但声明覆盖不匹配的产物同样拒绝。

## 4. 改动位置与复用

直接修改 run_test_gate.ps1 的各 Invoke/Assert 与汇总，不再造第二个文本解析报告器。保留当前 .NET/TRX、Unity XML 校验主体。提案引入薄工具 helper `tools/test-gate-result-contract.ps1` 供父与各脚本写/校验同一结果；PowerShell/BCL JSON/XML/hash/process 能力足够，不加依赖。配置仍由 test-gates.json 唯一拥有，schema 不另建 gate 清单。

脚本增加可选 ResultContextPath / ResultFilePath 参数；独立调用保持旧参数与业务行为，gate 调用必须提交显式结果。已有早期 exit、throw、超时、取消分支逐一写终态，成功记录只能在本脚本既有断言完成后产生。helper 不能只把 exit0 包装成成功。超时/杀子进程来不及写结果时由父写 Failed，保留日志。不能仅在父 wrapper 中假定 18 个旧脚本全完成了覆盖。

新增 helper 和下表所有脚本的最小结果参数/输出改动超出 Issue 列出的主要路径，属于本计划请求 Dot 明确确认的范围；若不接受，先审议逐脚本证据 adapter 替代方案，不能静默退回 exit0-only。

Unity：run-unity-compile-check.ps1 显式探测所请求版本或受管 DLL 参数；将实际 UnityManaged 和 RepoRoot 作为 MSBuild global properties 传入根镜像工程并验证到六个引用工程。子 csproj 的硬编码不会覆盖 global property；优先不改六个子工程。根 UnityCompileCheck.csproj 只在必要时加入参数缺失检查/传递，不改源选择/asmdef 语义。检查 Editor.Platform 外部二进制前提，缺失写 Blocked；旧 Unity Library DLL 必须有可核验来源，不冒充本次 Editor 验证。

## 5. 全部现存脚本兼容清单

脚本数 18，调用 21；计数由源码清单复现。以下只改 gate 契约输入/输出，不重构脚本业务或运行未授权性能/服务。

| 入口 | 使用 gate | 最小迁移/验证责任 |
| --- | --- | --- |
| `tools/validate_moba_codegen_ownership.ps1` | moba-codegen | 在既有检查/Check模式完成后显式报告声明覆盖；保留失败/early exit；不修改输入或业务规则 |
| `tools/audit_core_boundaries.ps1` | core-stability | 在既有检查/Check模式完成后显式报告声明覆盖；保留失败/early exit；不修改输入或业务规则 |
| `tools/run-unity-compile-check.ps1` | core-stability | 原缺安装 exit0 改 Blocked；传真实 MSBuild 参数和 mirror 产物身份 |
| `tools/audit_unity_package_dependencies.ps1` | core-stability | 在既有检查/Check模式完成后显式报告声明覆盖；保留失败/early exit；不修改输入或业务规则 |
| `tools/check_sample_baseline.ps1` | foundation-units | 在既有检查/Check模式完成后显式报告声明覆盖；保留失败/early exit；不修改输入或业务规则 |
| `tools/validate_moba_hero_acceptance_coverage.ps1` | moba-content-contracts | 在既有检查/Check模式完成后显式报告声明覆盖；保留失败/early exit；不修改输入或业务规则 |
| `tools/moba_business_id.tests.ps1` | moba-content-contracts | 保留现有正负断言；新增执行计数与终态，不用打印文本推断测试数 |
| `tools/moba_business_id.ps1` | moba-content-contracts | 在既有检查/Check模式完成后显式报告声明覆盖；保留失败/early exit；不修改输入或业务规则 |
| `tools/build_moba_content_report.tests.ps1` | moba-content-contracts | 保留现有正负断言；新增执行计数与终态，不用打印文本推断测试数 |
| `tools/build_moba_content_report.ps1` | moba-content-contracts | 保留现有数据/校验语义；输出移到本run目录，报告非空产物hash，不把生成图等同所有内容验收 |
| `tools/export_moba_content_ir.ps1` | moba-content-contracts | 保留现有数据/校验语义；输出移到本run目录，报告非空产物hash，不把生成图等同所有内容验收 |
| `tools/build_moba_content_graph.ps1` | moba-content-contracts | 保留现有数据/校验语义；输出移到本run目录，报告非空产物hash，不把生成图等同所有内容验收 |
| `Server/Orleans/tools/run_shooter_multiprocess_smoke.ps1` | shooter-multiprocess, shooter-multiprocess-compatibility, shooter-multiprocess-soak | 保留现有 domain/阈值断言；绑定已有输出与被加载DLL；显式报告缺工具/NoBuild前提；本轮不实际运行这些任务 |
| `Server/Orleans/tools/test_shooter_multiprocess_ownership_cleanup.ps1` | shooter-multiprocess-ownership-cleanup | 保留现有正负断言；新增执行计数与终态，不用打印文本推断测试数 |
| `tools/run_runtime_benchmarks.ps1` | runtime-performance-measurement | 保留现有 domain/阈值断言；绑定已有输出与被加载DLL；显式报告缺工具/NoBuild前提；本轮不实际运行这些任务 |
| `tools/run_shooter_aoi_lod_gate.ps1` | shooter-performance | 保留现有 domain/阈值断言；绑定已有输出与被加载DLL；显式报告缺工具/NoBuild前提；本轮不实际运行这些任务 |
| `Server/Orleans/tools/run_moba_smoke.ps1` | moba-smoke | 保留现有 domain/阈值断言；绑定已有输出与被加载DLL；显式报告缺工具/NoBuild前提；本轮不实际运行这些任务 |
| `Server/Orleans/tools/run_moba_multiprocess_smoke.ps1` | moba-multiprocess | 保留现有 domain/阈值断言；绑定已有输出与被加载DLL；显式报告缺工具/NoBuild前提；本轮不实际运行这些任务 |

所有 130 配置步骤、32 gate 受统一聚合影响；6 个配置 kind 是 dotnet-build(8)、dotnet-test(73)、powershell-script(21)、gate(9)、unity-editmode-test(18)、unity-playmode-test(1)。运行器支持但当前配置没有的 unity-execute-method 也校验结果身份及声明覆盖，不遗漏分支。

## 6. 待 Dot 明确的三项选择

1. core-stability 的 Unity mirror optional 政策及全 Unity gate required；若改 required，该机缺环境结果应阻塞整个 core-stability。
2. 聚焦运行缺完整覆盖时 gate NotRun/exit4 的 CLI 兼容变化。
3. 薄 helper + 18 个脚本最小结果输出参数/终态改动；不改它们的玩法/性能判据、单独入口业务语义，不新增依赖。

本轮先发布此计划/schema 的 draft PR，并在 #6 首次反馈以上来源、采用合同、兼容和未决项。批准到来前保持 planning，生产脚本、gate 配置与测试规范保持原样。
