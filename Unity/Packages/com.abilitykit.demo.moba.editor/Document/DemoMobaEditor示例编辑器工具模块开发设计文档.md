# Ability-Kit Demo MOBA Editor 示例编辑器工具模块设计

> **阅读对象**：维护 MOBA 示例配置资产、诊断窗口、场景预览、热重载菜单和帧同步测试工具的 Editor 开发者。
>
> **文档目标**：说明本包的职责、依赖方向与维护入口。具体使用方法见 [工具指南](README.md)。

---

## 一、设计理念

Demo MOBA Editor 是 MOBA 示例的 Unity Editor 工具层。它集中配置 ScriptableObject、JSON 导入导出、场景预览、战斗诊断、Scene Gizmo、帧同步测试和热重载入口，但不承载正式战斗逻辑。

战斗诊断遵循只读客户端边界：Runtime 采集真实业务事实并投影为不可变 DTO，Editor 通过 `IBattleDiagnosticReadOnlySession`、ViewModel 和 `IBattleDebugPanel` 展示，不把活动 Runtime 对象作为诊断详情的数据源。

---

## 二、模块边界

负责：

- 定义 Character、Skill、Buff、Projectile、VFX 等配置 ScriptableObject。
- 提供配置资产、JSON 导入导出、文件夹同步和校验。
- 提供 `BattleDebugWindow`、Panel Registry、诊断 Session 解析、ViewModel 及 Actor 详情、事件、状态和帧同步面板。
- 提供 MOBA Demo 场景创建、刷新、打开与播放入口。
- 提供 CollisionWorld 与战斗范围 Scene Gizmo。
- 提供 `EditorGameFlowPumpWindow`、`FrameSyncTestWindow`、Hot Reload 菜单和 Unity 日志适配。
- 承载 Editor-only 测试程序集，覆盖 Diagnostics Core、Runtime 采集/Store/Session 和 Editor ViewModel。

不负责：

- 不参与 Player 或服务端运行时发布。
- 不定义活动战斗对象的所有权，也不从 Editor 修改生命、属性、Buff、Effect 或技能状态。
- 不在 Editor 程序集中定义通用诊断 DTO；平台无关 DTO、查询状态和 Store 契约位于 Runtime 包的 `DiagnosticsCore`，并保持 `noEngineReferences`。
- 不负责服务端配置加载、远端诊断传输或离线诊断文件解析。

---

## 三、依赖与维护约束

- 依赖方向保持 `Editor -> MOBA Runtime -> Diagnostics Core`，禁止 Runtime 反向引用 Editor。
- 配置导出 JSON 后需要与对应 Runtime/Share 数据契约保持一致。
- Battle Debug 面板是只读调试视图，不作为正式游戏 UI，也不绕过 Session 直接访问 Store。
- 新增诊断能力时，按 [扩展开发指南](EXTENDING.md) 完成 DTO、Store、Producer、窄端口、Session、ViewModel、Panel 和测试闭环。
- 当前能力与未实现范围以 [当前能力与限制](CURRENT-CAPABILITIES.md) 为准；验证口径以 [测试与验证](TESTING.md) 为准。
- 查询状态、Session 或 Unity 工程异常按 [故障排查](TROUBLESHOOTING.md) 定位。
- 长期架构见 [MOBA 战斗诊断与溯源工具设计](Moba战斗诊断与溯源工具设计.md)，批次落地见 [实施历史](IMPLEMENTATION-HISTORY.md)。

---

*文档版本：2.0*
*最后更新：2026-07-19*
