# 用餐占桌与服务结束：设计

需求见 [prd.md](prd.md)。未批准前不改产品代码。

## 前厅

`AdvanceGuests` 看到订单 `Completed` 时，把桌子改成用餐并把已坐 tick 归零。之后每一拍累加，达到 `DiningTicks` 才离席。

`Unsatisfied` 和尚未开单的超时仍立刻离席。`CanSucceed` 不变：收尾、无人在座、伙伴空闲。

## 宿主

新增 `TryFinishService`。没有挂前厅时拒绝，不改生命周期。挂了前厅但 `CanSucceed` 为假时拒绝，关卡仍是 `Running`。为真时调用现有 `BeginEnd(Success)`，不自动 `CompleteEnd`，好让结算确认仍发生在结束之前。

直接 `BeginEnd` 不改，旧闭环不用先跑前厅。

## 不做

不写收益。不把用餐写进结算文件或大关检查点。
