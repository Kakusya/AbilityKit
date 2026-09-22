# 用餐占桌与服务结束：实施清单

需求见 [prd.md](prd.md)，设计见 [design.md](design.md)。未批准前不改产品代码。

## 顺序

1. 订单 `Completed` 进入用餐；`Unsatisfied` 仍马上离席。
2. 测试用入口把一张已开订单标成完成，不写结算。真实提交仍走 `SubmitOrder`。
3. 宿主 `TryFinishService` 只在 `CanSucceed` 时进入 `Ending`。
4. 补 spec、Todo、progress。跑 `cooking-kitchen-loop` 与 `cooking-et-level-runtime`。

## 用例

- G01：用餐 2 tick。完成后第一拍仍在座且不能成功，第二拍离席后可以。未满足的桌子同一拍就空。
- G02：宿主座位未空时 `TryFinishService` 拒绝；空了之后进入 `Ending`。没有前厅时拒绝且状态不变。
