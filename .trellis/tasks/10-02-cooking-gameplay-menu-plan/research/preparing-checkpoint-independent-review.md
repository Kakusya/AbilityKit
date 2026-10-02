# Preparing checkpoint 独立只读复审

2026-10-02。integration `585e15982`之后当前WIP，未.NET、未生产编辑。

## 阻断：零营业clock没有禁止伪造前厅营业状态

当前Restore仅要求Preparing前厅ServiceTicks=0及ServiceStartLogicalTick=0，未要求顾客/工作/桌状态是从未营业的初态。可从合法Preparing checkpoint设置NextCustomerSequence=1，加入customer-1/table-1、WaitingForInquiry、elapsed0、null order/template，Tables中的table-1改Occupied，其余idle/clock0/closingfalse/workempty保持。前厅自身counter/customer/table验证允许该组合；host随后Bind并Adopt，准备态恢复接受本不可能由准备Tick生成的顾客。

Closing=true在ServiceTicks0时由前厅counter关系拒绝，但不能代替上述营业客户检查。联合篡改Recipe订单/前厅Ordered客户也应显式拒绝；不能仅凭clock0判断准备未服务。

建议在Preparing的host恢复前置阶段检查前厅pristine：没有Customers/Work/WashQueue/UnsatisfiedOrders、NextCustomerSequence和arrival计数初值、Idle且elapsed/required/经验为0、所有Tables Free/ClearSequence0、Closing=false。保留合法厨房准备供货/加工状态；不要粗暴禁厨房原料和进度。新增毒化客户/桌状态及联合订单反例，断言在freshfactory.Create之前拒绝和原owner不变。已先向root报告，当前不直接修改。

## 确认的新恢复路径与控制证据

- Export允许已初始化Preparing、无pending/inflight；0tick发布已绑定Recipe.LevelScope，测试明确验证无需先Tick即可export/restore。
- Restore允许Preparing/Running但拒绝其它State、Outcome非null、InstalledLayout非null，并在创建厨房前验证policy身份/offset和scope。Preparing重建走BeginPreparation而不Complete/Start，保留Preparing状态与唯一factory厨房；Lifecycle版本adopt现允许Preparing，且不能倒退版本。
- 新真实控制例先请求/收货、拆份、留下第二pending供货和elapsed1的加工，保存Preparing checkpoint；原owner沿ContinuePreparation接收pending、继续加工、Complete→Start→Running，保存不中断final。恢复臂codec/host重建后同样执行helper，比较最终完整CanonicalText。这次确有真实不中断control，纠正上轮仅立即恢复相等的证据边界。
- 0tick例保留无顾客/营业clock0并检查factory只创建一次；非法State/Outcome/layout/offset负例检查CreateCount0。上述负例尚不包含本次发现的伪造前厅初态。

## 结论

控制恢复覆盖明显增强，但准备前厅状态漏洞修复前不接收完整Preparing checkpoint增量。InstalledLayout仍null且几何安装另行推进。没有凭测试数量标S06/S07/S08/S14完成。

Lint/TypeCheck/Tests本审阅未执行；root完整门禁正运行，不能把静态结论冒充独立跑测。

## Resolution：前厅初态恢复漏洞已封闭

后续只读复审核对：Restore在创建Lifecycle/host/factory厨房之前，以可信Front配置重新构造全新前厅，安装同Flow/manual policy并绑定同Menu，比较完整前厅checkpoint CanonicalText。Preparing要求与此pristine完全相同；没有可信Front配置时pristine=null，前厅payload必须null。因此客户、桌位、工作、洗碗队列、成长、arrival计数等全部营业字段不能只靠clock0掩饰，不需要维护易漏字段的手写清单。

新测试包含原customer-1/Occupied毒化反例，并确认拒绝且fresh CreateCount0；还含arrivalclock、伙伴成长、ClearSequence、Closing变体及无可信front却附带payload，均要求创建厨房之前拒绝。原合法Preparing供货/部分加工控制恢复测试保留，不把厨房备料误当营业污染。

root实际红测试0/1先证明原漏洞，producer修复后报告38聚焦通过；本复审未重跑.NET，等待root两完整门禁。**原阻断已解除，Preparing checkpoint受限增量可接收。** 初始报告保留作审计历史，InstalledLayout与其它单机出口边界保持。
