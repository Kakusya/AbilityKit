# S06 F08 服务目的地增量

2026-10-02，integration在83be569cd+已审阅S06 WIP基础上修改。仅授权段：CookingFrontOfHouseConfiguration可信policy/Freeze、CookingLevelEtHost绑定/验证、CookingRecipeLoop内部只读predicate与SubmitOrder首个mutation前hook、新CookingFrontDeliveryEtTests、S06design最小合同追加。未提交，保留他人WIP，不改geometry。

## 生产行为

新增 optional CookingFrontDeliveryPolicy(Mode,ServingAnchor)：ServingAnchor/CustomerTable；null保留旧通用提交。配置Freeze要求enum有效、ServingAnchor有非空anchor、CustomerTable不带自报anchor。canonical/Identity自动包含字段；Level6候选已required FrontOfHouseConfigurationIdentity继续比较可信factory身份，不相信checkpoint自身hash。

host在厨房绑定时要求空间存在及目标anchor唯一，拒绝同名跨LocationKind歧义；CustomerTable验证所有table anchors，ServingAnchor验证指定点。绑定readonly predicate捕获当前新kitchen/house，按current Order→Ordered customer→实际TableId派生目标，ServingAnchor也要求customer仍可服务。使用existing ValidateSpatialReach当前pose/朝向/距离/视线。不存在Ordered顾客→OrderRejected；目的地不可达→TargetOutOfRange。没有新增command目的地字段，不更改S05物件ownership、洗碗、配方/贴票要求或结算。

恢复及每次front重新绑定走同一配置与新callback；old无policy/no-front场景沿旧逻辑。没有加入惩罚、正常业务失败或贴票工位强制，不是S14或Unity交付。

## 实际专属ET证据

`dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter FullyQualifiedName~CookingFrontDeliveryEtTests --nologo --verbosity quiet --logger trx`

初次编译失败为新test误读snapshot.Removed（该公开snapshot只枚举未移除物件），改用IsProduct查询。日志保留 `local/Logs/cooking-execution/front-delivery-first-build-failure.log`；包含既有shared Network/LiteNet warnings，未抑制。

第二次：5pass/1fail。离席拒绝测试在同一真实ET Tick里另一张开放订单自然超时，所以全orders相等断言并非本命令副作用；改为等全部顾客已离席的稳定控制，不改变生产规则。失败log/trx保留 `front-delivery-first-test-failure.*`。

目前实际最终6/6pass、0skip，incremental编译输出无本增量warning/error，证据 `local/Logs/cooking-execution/front-delivery-final.log` / `.trx`。raw经StartProcess/固定Tick产生真实成品→Pickup/PutIn盘/Pickup盘，无注入中间成品。覆盖remote拒绝、两个同recipe订单wrongtable、wrongfacing、正确桌成功一次、已拒绝原command在走近后仍去重拒绝且snapshot不变、Serving/Table两mode dispose/restore重绑再远端拒绝→真实Move到点成功、可信factory策略身份变化拒绝、legacynull远端保持旧行为、离席旧order由现有Open状态前置拒绝、invalidenum/字段/缺anchor/跨Kind歧义anchor严格错误。

离席控制目前Serving模式；CustomerTable同场景可追加Theory控制，已向root协调避免其组合gate读取源码时并发编辑。全文不预称其通过。完整S06/helper/menu组合门禁由root独立执行；未据此标S06/S14全部完成。

## 最新最终证据：两种模式离席控制

按root追加要求，在其确认integration空闲后将离席test改成ServingAnchor/CustomerTable Theory两控制，均真实Move到相应目的地、等顾客全部离席再Submit旧订单，拒绝且食物/hand/结算/订单保持不变。该拒绝沿既有非Open订单前置，不声称重新发明离席机制。修回三份existing源文件单EOF newline；针对本增量所有源 `git diff --check` exit0，仅CRLF正规化提示。

修后最终focused **7/7 pass、0skip**，incremental输出无warning/error；`local/Logs/cooking-execution/front-delivery-both-modes.log` 与 `.trx` 已复制。前文6/6是此Theory控制追加之前的真实历史证据；此处覆盖当前最终源。已明确通知root integration.NET窗口释放、源稳定，未提交。root接独立审阅/组合gate，不预称其通过。
