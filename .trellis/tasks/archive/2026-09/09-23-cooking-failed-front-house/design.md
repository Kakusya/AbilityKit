# 失败重开丢掉前厅现场：设计

需求见 [prd.md](prd.md)。未批准前不改产品代码。

## 前厅

新增 `DropFailedScene()`。不接收厨房。桌子、未满足、营业计数、收尾、到店序号、伙伴工作和洗碗队列全部丢掉。不调用 `OpenOrder` 或 `CompleteWash`。

`ResetForNextLevel` 保持成功路径：先收完进行中的工作，再清座位。

## 宿主

`CreateRetry` 在新厨房被接受后调用 `DropFailedScene`。拒绝或抛错之前不调用。`CreateSuccessor` 仍调用 `ResetForNextLevel`。

## 不做

不发明失败条件。不改结算文件。
