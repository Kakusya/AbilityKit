# 两个固定 Flow（dot技术接受；规则仍Draft）

来源：[完整dot计划](dot-plan-reply-raw.txt)、[具体API](accepted-api-design.md)。初始化只装配fixture，后续行为走正式命令。

## compete-one-item / v1

初始化 → 初始观察/联网就绪 → 两动作 barrier → 两正式结果 → authority OWN/REJECT → network pickup 收敛 → 胜者精确 retry → IDEMP → 收尾。
把 Drop 从这条首例移到独立 pickup-drop-one-item/v1：单角色 Pickup → 观察/必要收敛 → 新 ID及新版本 Drop → authority及必要客户端目标观察 → 收尾。这样复用是实在的，也不会把两个目标的证据窗口混在一起。


## 操作与观察窗口

两个空手、有资格、合法可达角色、一份目标、同一初始item version。offline同批同步入队后唯一Tick；network各client Arm/Release并batch0正式发送，不保证同frame。等待两真实业务终态，任一合法胜者均可，协议拒绝不冒充领域拒绝。

Authority同次复制独立ItemInHand和item location；network Pickup后且下一item动作前检查已安装完整目标投影。Replay精确保留原command/batch/version/payload，只换CallId。第二独立Pickup/Drop用新BusinessId与新版本。先保留首次失败现场再归位；缺目标/证据或未知writer不能Passed。JSON只选择已注册C#Flow，不做任意代码/DSL或模型决策。
