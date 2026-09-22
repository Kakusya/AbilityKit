# 前厅询问、洗碗与小关时间结构：实施清单

需求见 [prd.md](prd.md)，设计见 [design.md](design.md)。未批准前不改产品代码。

## 顺序

1. 读 `trellis-before-dev` 与 cooking spec index，把前厅规则补进现有 recipe-loop spec，不另起平行文档。
2. 新增前厅对象和一步推进。先写失败用例：没问完不开单、忙碌时不并行、收尾不来客、未走完不能成功。
3. 询问到点调用 `OpenOrder`。洗碗到点调用 `CompleteWash`。待询问优先于洗碗。
4. 等待上限把仍开放的订单记为未满足。需要一条只关闭订单、不产生结算的厨房入口；没有这条入口之前不直接改订单字典外面的状态。
5. 成功查询为真时才允许走前厅驱动的成功收口。收口先完成进行中的询问和洗碗。
6. 宿主在运行帧提交后调用同一步。暂停帧不调用。
7. 补 spec 修约、`Docs/Todo.md` 和 `progress.md`。收益、评价、失败条件、伙伴成长保持未完成。
8. 跑下面两个既有门禁。不改 `tools/test-gates.json`。

## 验证

```text
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
```

领域测试挂 `Gate=CookingKitchenLoop`。宿主测试留在 ET runtime 测试程序集。

## 用例

- F01：两张桌子，间隔 2，询问 2，清洗 2。第一位占桌后不开单；伙伴走完询问 tick 后订单簿出现这一单。第二张空桌按间隔再占。两单都按到店顺序开出。
- F02：伙伴询问中提交一只脏碗。询问结束前碗仍脏。询问结束后下一空闲段才开始洗，洗完回到干净池。
- F03：营业 4 tick，之后进入收尾。收尾帧桌子不再增加。已开单顾客用餐到点离席。全部离席且伙伴空闲后才允许成功。
- F04：等待上限到了仍没送到。订单记为未满足，不出现结算条，关卡不进入失败。
- F05：成功收口前有一条询问差 1 tick、一只碗差 1 tick。收口后订单已开、碗已干净，伙伴为空闲。
- F06：宿主运行帧会推进前厅；暂停帧不推进。已有直接 `OpenOrder` 的闭环测试仍通过。

## 回退点

- 不把未满足写成 `CookingOrderSettlement`。
- 不改 `BeginEnd` 的现有调用，避免旧闭环必须先跑前厅才能成功。
- 若前厅一步在 `OpenOrder` 失败后仍把桌子标成已开单，撤回完成分支，桌子留在待询问。
