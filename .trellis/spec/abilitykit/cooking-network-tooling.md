# Cooking 本机防火墙与共享端口工具

本文件是按需读取的操作说明，迁自根 AGENTS。先遵守 [Owner 约束](../../../AGENTS.md)：当前 Cooking 工程保持暂停，历史命令不提供执行授权；Owner 要求不测试时不得运行下列测试或验收入口。禁止 SHA 校验，禁止复制测试原始输出或新增 JSON 证据。

## 工具与调用约定

- 源码入口：[cooking-firewall.ps1](../../../tools/cooking-firewall.ps1)，默认配置：[cooking-network.defaults.json](../../../tools/cooking-network.defaults.json)。只服务 Cooking Windows 联网测试；默认开放 **UDP 18090–18099**，Private/Public 网络类别，来源限制为 LocalSubnet。不切换网卡类别、不关闭防火墙，不自动开放 TCP。
- 管理员终端配置：`powershell -ExecutionPolicy Bypass -File tools/cooking-firewall.ps1 -Action Open`。自定义范围同时指定 `-StartPort` 与 `-EndPort`。只有规则实际写入并核对后，才保存到 `%LOCALAPPDATA%\AbilityKit\CookingNetwork\ports.json`；各 worktree 共享本机这份配置，后续工具读配置，不把端口常量复制到各工作树。参数覆盖已保存配置，已保存配置覆盖随工具分发的默认值；损坏或不支持的配置报错，不静默回退。
- 固定位置安装：`powershell -ExecutionPolicy Bypass -File tools/cooking-firewall.ps1 -Action Install`，将工具及默认配置复制到 `%LOCALAPPDATA%\AbilityKit\CookingNetwork\bin`，向当前用户 PATH 去重追加目录；新开终端后运行 `cook-firewall -Action Show` 或 `cook-firewall -Action Check`。固定安装无常驻服务；PATH 只负责找到命令，不授予 worktree 子进程防火墙权限。
- Windows Cooking 联网测试现在自动调用 **`GetPort`**：`run-cooking-network-process-acceptance.ps1`、`run-cooking-network-concurrency-acceptance.ps1`、`run-cooking-network-rich-recovery-acceptance.ps1`、`run-cooking-network-process-measurement.ps1` 与固定 Flow 的 Network 模式均在启动房主前取端口。优先调用 `%LOCALAPPDATA%\AbilityKit\CookingNetwork\bin\cooking-firewall.ps1`，未安装时调用当前仓库工具；两者读取同一份本机配置。包装脚本默认 `-Port 0` 表示自动取端口，显式非零 `-Port` 保留调用方选择；Client 只使用房主端口，BuildOnly 不取端口。
- 自动路径将 JSON `port` 直接交给房主，核对实际 `READY` 的端口与进程身份后再启动客户端。端口选择仍只由工具查询共享配置和 UDP 占用；`portReserved=false` 表示未保留候选。范围耗尽、工具失败、绑定冲突或 READY 不一致均明确失败；重新运行测试会重新取端口，不静默退回随机端口或范围外端口。选择与实际端点证据保存在运行目录。手动启动仍先执行 `cook-firewall -Action GetPort`。
- `Show` 读取配置；`Check` 核对本机规则。两者只读，可通过 `-ProgramPath` 检查确切 Cooking EXE 的阻止规则。显式 Block 优先于 Allow；若需修复该程序冲突，管理员使用 `Open -ProgramPath '<实际 Cooking EXE 完整路径>' -RepairProgramBlock`，先备份，再仅从该程序的本地 UDP 阻止规则扣除配置端口范围，保留范围外端口和原 TCP 规则；不删除其他应用或策略规则。输出提供备份路径，回退使用 `-Action RestoreBlock -BackupPath '<备份路径>'`。
- 默认 Check，操作输出 JSON，失败返回非零退出码；`-ConfigPath` 用于显式隔离配置或测试。`Show` 与 `GetPort` 只读配置／端口占用，不证明防火墙或远端连通。规则核对、端口占用、应用监听、远端实际连通是不同事实；不能用 `Test-NetConnection -Port` 的 TCP 测试证明 UDP。缺少实际物理双机证据时连通性保持 NotRun/NOT_VERIFIED。现有房主输出 `READY <实际端口> <PID>`；本工具获取候选端口并管理防火墙及范围配置，尚不负责房主启动／关闭／查询或端口保留。

## 历史验证入口（不提供执行授权）

下列命令只保留已有工具的定位信息，不属于默认门禁。上方的旧 rich 调用说明也仅是历史集成事实；旧 rich／NetworkRichRecoveryAcceptance 禁止执行。Cooking 产品验收仅按 Owner 指定的 [FlowAcceptance 场景](../../../Docs/design/CookingGame/testing/fixed-flow.md) 路由，不能从本文件派生新控制套件。

- 聚焦控制：`powershell -ExecutionPolicy Bypass -File tools/cooking-firewall.tests.ps1`。测试使用隔离模拟，不修改真实防火墙或用户 PATH；本机应用另保留规则、端口过滤器、配置与原始退出码证据，不把它当产品 LAN 验收。
- 集成控制：`powershell -ExecutionPolicy Bypass -File tools/cooking-test-ports.tests.ps1`；固定流程回归：`dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -m:1 --filter "FullyQualifiedName~CookingFixedFlowTests|FullyQualifiedName~CookingNetworkTestPortTests"`。这些测试启动时只读取配置与端口占用，不自动调用 Open/Install。离线模式、非 Windows Flow、专用 impairment relay 与进程内传输 fixture 保留各自运行语义；同机通过仍不证明物理双机 LAN。

