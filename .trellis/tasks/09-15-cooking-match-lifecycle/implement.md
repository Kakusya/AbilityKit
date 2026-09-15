# P4 关卡/地图/Match 生命周期：实施清单

> 以下均为迁移的**未执行**规划。任务进入 `in_progress` 前必须重新审阅依赖、阻塞决策、当前代码与验证命令；不得只因文档迁移而勾选或关闭任何项目。

## 迁移前置条件

- 依赖：P0 identity/location/lifecycle、P1 session、P3 config/layout 校验。
- 阻塞：房间人数、房主退出、断线恢复和主机迁移仍待产品决策。
- 规划验证：M01-M08（均为 future 规划，尚未执行）
- 实施前将 `.trellis/spec/abilitykit/index.md`、`.trellis/spec/abilitykit/validation.md` 与本任务对应 cooking spec 加入 context manifests。

## 迁移的原实施清单

## 规划状态

以下任务均为未执行的实施规划，当前保持未勾选；checkbox 应在未来实施并完成对应验证后更新，不是本 change 的永久验收条件。

## 1. 地图与准备上下文

- [ ] 1.1 复核阶段 1/P0、阶段 2/P1 和阶段 4/P3 的实际实现/证据边界，并实现最小 Unity authoring→逻辑 layout 导出 seam；验证：future Unity EditMode 成功场景从 authoring fixture 导出稳定逻辑 layout manifest，断言处理站、容器/槽位、食材/菜品和放置位类别及逻辑 identity 正确，且不使用 GameObject identity 作为网络/模拟身份；失败场景对缺失/重复/非法 authoring 节点给出诊断并拒绝导出。
- [ ] 1.2 实现配置/布局/session identity 的准备前校验与原子准备结果；验证：M01 合法 Level 进入 Ready，M02 缺引用、非法布局或 hash mismatch 保持非 Ready 且不创建 gameplay 实例。

## 2. Match 生命周期

- [ ] 2.1 实现 Preparing→Ready→Started→Ended 的稳定迁移、结构化拒绝与事件；验证：M03 完整操作序列逐态通过，M04 非法重复开始/Ended 后命令状态和事件不变。
- [ ] 2.2 实现 Ended→新局的隔离 instance/epoch 创建和旧局封闭；验证：M03 新局可准备，M06 旧命令/快照不能改变新局状态。
- [ ] 2.3 实现多 Match 的实体、recipe/订单进度、计时器和版本隔离；验证：M05 两实例并行 Tick/命令产生互不污染的双状态 trace。

## 3. 快照与表现接入

- [ ] 3.1 定义并导出包含 Match identity、session/epoch、config identity、生命周期状态和 logical version 的业务快照；验证：M07 按序生命周期快照可应用，旧/乱序/未知输入不覆盖新状态。
- [ ] 3.2 接入 Unity 只读 projection、authoring→逻辑 layout 导出后的加载与场景 smoke，禁止场景对象自行推进 authority；验证：future Unity EditMode 成功/失败场景断言导出的 layout 可加载、旧/未知 layout 输入不覆盖新状态、导出诊断可审阅且 authority 不变。

## 4. 分层集成与阶段出口

- [ ] 4.1 建立 future .NET lifecycle contract 与同机多实例 runner；验证：M01-M07 全部成功/失败断言及 logs、state/snapshot trace 齐全。
- [ ] 4.2 在阶段 2/P1 session、LAN 集成及 D1-D4 决策门满足后执行两 PC 生命周期验收；验证：M08-M09 覆盖准备、开始、结束、重开，真实 LAN 证据不可由同机替代，缺门时保持 blocked。
- [ ] 4.3 执行受影响 `runtime-contracts`/`core-stability` 门禁与 `git diff --check`；验证：交付记录真实 pass 或环境 skip，不把规划测试当已通过。
- [ ] 4.4 将 P4 交付证据链接到后续阶段 6/P5 和阶段 7/P6 的现有规划文档；验证：引用 `../add-cooking-persistence-management/proposal.md` 与 `../add-cooking-network-measurement/proposal.md` 作为后续依赖指针，不把规划当作实现证据。
