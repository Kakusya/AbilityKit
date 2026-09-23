# 交接前收完正在询问的顾客：实施清单

需求见 [prd.md](prd.md)，设计见 [design.md](design.md)。未批准前不改产品代码。

## 顺序

1. 把 `FinishInProgress` 挪到 `ExportSuccessHandoff` 之前。
2. 断言下一关订单簿为空，清除计数包含刚开出的单，洗完的碗仍干净。
3. 补 spec、Todo、progress。跑两个既有门禁。

## 用例

- H07：宿主营业中入座并开始询问，不等问完就成功交接。下一关订单为空。清除订单数为 1。若同时有一只待洗碗，交接后它是干净的。
