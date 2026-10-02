# Orca worker：网络 N01–N03

用户明确授权在总plan之后实施单机与联网、worktree并行、测试通过合并master，Unity后置；未提细节授权助手。你不是唯一开发者，不能撤销他人改动；只在自己的Orca worktree工作，不merge master/push，不创建额外并行子agent。

## Target / Ownership

先完整N01对齐真实仓库和旧决定，然后在现有联网能力上推进N02/N03。拥有 src/AbilityKit.Game.Cooking/Session/、网络专属测试/Harness、新独立process网络验收工具、N01/N02/N03 artifacts；可精简同步 ADR/long-term-goals.md、ADR-0002附注、09-19 task网络纪要路由与相关network spec，不重写历史。不要改RecipeLoop/Domain/ContentCatalog/Checkpoint/ConfigValidator核心文件、AGENTS或总execution dashboard，不改Unity。若通用LiteNet/NetworkHost有真实缺口，先给协调者精确边界/patch请求，避免与应用程序集边界冲突。

## N01 facts and decision

旧09-21 KCP方向与较晚09-24 notes第28/29节、旧planning PRD显式确认通用LiteNet reliable-UDP唯一真实网络（非TCP），现有Cooking.csproj也引用LiteNet。按reference来源优先记录较晚owner确认以及当前代码为已选现有基线，协调过时权威附注，不当新架构选型。复用 IChannelListener/IServerChannel/NetworkHost/ServerNetworkSession 与 InProcess 本地Client connection，不建 Cooking独立UDP transport，不删除旧源码（用户本轮无删除指示）。

固定Tick authority、network仅解帧入队、完整快照、Client无玩法写权限/首版无预测。将“先到先得”明确为权威接收序号输入和稳定模拟顺序，不依赖线程/字典枚举；重复/stale scope/epoch/sequence与冲突必须明确。

## N02/N03 change

检查existing CookingSessionHost/Client真实生产链路，前厅snapshot/checkpoint、level transition choices、local host与remote命令、pause/断线丢未消费命令、reconnect token与baseline consensus。完成必要漏洞/未接入状态，强类型serialize roundtrip并保持host唯一模拟。核心worker正新增Move/manual work/ServePortion/Clear/Discard，稍后将提供schema和commit；先做独立session边界/测试，然后将其完整payload/snapshot传输纳入实际UDP，同一业务命令不做单独网络玩法。

至少实际**独立Host和Client进程**经通用LiteNet UDP跑厨房/订单/前厅/跨Level/争抢/重新绑定与完整基线恢复，含延迟/乱序/重复/旧epoch/旧Level拒绝/队列丢弃等证据；测试中的fake topology必须清楚标InProcess，不能冒充网络。测量实际命令响应、快照/字节、异常恢复，阈值为可重复测试fixture而非正式性能承诺。必须有运行工具可设置远端IP，供以后两PC LAN复验。

Orca host list 当前只有local，environment list为空；**真实两台物理PC证据受环境限制**，不能记录pass。不要停在这个障碍：实现代码、同机独立进程UDP验证和portable dual-host runner全部可完成，将唯一剩余环境验收具体记录。不得把整体N02/N03标成完整双机通过。

## Before coding / Acceptance

读取自己task artifacts与spec（lan-session、network-measurement、match lifecycle）、reference/ADR、Protocols和验证规则；细化真实API/错误矩阵/恢复与测试证据，获授权范围可实施不重复问用户。Git identity Kakusya，缺本机身份可初始化，按task状态推进。

运行真实相关Cooking测试、ET适用gate、若碰通用Network则runtime-contracts/包边界检查。日志大量warning重定向，不能只报告测试名字或样例成功；报告实际数量/exit/拓扑/路径/commit。代码独立提交branch，向协调者提供core同步所需schema/messages及最后环境缺口，按Orca preamble生命周期结束。
