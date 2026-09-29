using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

public readonly record struct CookingCustomerId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct CookingCompanionId(string Value)
{
    public override string ToString() => Value;
}

public sealed record CookingFrontOfHouseMenu
{
    public CookingFrontOfHouseMenu(IReadOnlyList<OrderTemplateId> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        if (templates.Count == 0 || templates.Any(template => string.IsNullOrWhiteSpace(template.Value)) ||
            templates.Distinct().Count() != templates.Count)
        {
            throw new ArgumentException("A front-of-house menu requires unique, nonblank templates.", nameof(templates));
        }
        Templates = templates.ToArray();
    }

    public IReadOnlyList<OrderTemplateId> Templates { get; }

    public static CookingFrontOfHouseMenu Single(OrderTemplateId template) => new(new[] { template });
}

/// <summary>
/// 一位固定伙伴的前厅节奏：问完才开单，没有询问才洗碗。
/// 伙伴成长只覆盖当前 Level 的任务计数与洗碗加速；收益、评价和失败条件不在这里。
/// </summary>
public sealed class CookingFrontOfHouse
{
    public static readonly CookingCompanionId DefaultCompanion = new("companion-1");

    private readonly CookingFrontOfHouseSchedule _schedule;
    private readonly CookingCompanionId _companion;
    private readonly List<CookingTable> _tables = new();
    private readonly Queue<ItemId> _washQueue = new();
    private readonly HashSet<ItemId> _queuedBowls = new();
    private readonly List<OrderId> _unsatisfied = new();
    private CookingCompanionWork _work = CookingCompanionWork.Idle();
    private int _serviceTicks;
    private int _ticksUntilNextGuest;
    private bool _closing;
    private int _nextGuest;
    private int _completedCompanionTasks;
    private OrderTemplateId[] _orderMenu = Array.Empty<OrderTemplateId>();

    public CookingFrontOfHouse(CookingFrontOfHouseSchedule schedule)
        : this(schedule, DefaultCompanion)
    {
    }

    public CookingFrontOfHouse(CookingFrontOfHouseSchedule schedule, CookingCompanionId companion)
    {
        ValidateSchedule(schedule);
        if (string.IsNullOrWhiteSpace(companion.Value))
            throw new ArgumentException("A companion identity is required.", nameof(companion));

        _schedule = schedule;
        _companion = companion;
        _ticksUntilNextGuest = 0;
        for (var index = 1; index <= schedule.TableCount; index++)
            _tables.Add(new CookingTable($"table-{index}"));
    }

    public bool IsClosing => _closing;

    public bool CompanionIdle => _work.Kind == CookingCompanionWorkKind.Idle;

    public IReadOnlyList<OrderId> UnsatisfiedOrders => _unsatisfied.ToArray();

    public int SeatedCount => _tables.Count(table => table.Customer is not null);

    public bool CanSucceed =>
        _closing && SeatedCount == 0 && _work.Kind == CookingCompanionWorkKind.Idle;

    public CookingFrontOfHouseSnapshot Snapshot()
    {
        var customers = _tables
            .Where(table => table.Customer is not null)
            .OrderBy(table => table.Id, StringComparer.Ordinal)
            .Select(table => table.Customer!.Snapshot(table.Id))
            .ToArray();
        return new CookingFrontOfHouseSnapshot(
            _schedule,
            _serviceTicks,
            _ticksUntilNextGuest,
            _closing,
            _nextGuest,
            _orderMenu.ToArray(),
            customers,
            new CookingCompanionSnapshot(_companion, _work.Kind, _work.TargetCustomer, _work.TargetTable,
                _work.Bowl, _work.Elapsed, _work.RequiredTicks, _completedCompanionTasks, WashSpeedUnlocked),
            _washQueue.ToArray(),
            _unsatisfied.ToArray());
    }

    public CookingFrontOfHouseCheckpoint ExportCheckpoint(OrderTemplateId activeOrderTemplate)
        => ExportCheckpoint(CookingFrontOfHouseMenu.Single(activeOrderTemplate));

    public CookingFrontOfHouseCheckpoint ExportCheckpoint(CookingFrontOfHouseMenu menu)
    {
        BindMenu(menu);
        return new CookingFrontOfHouseCheckpoint(_orderMenu[0], Snapshot());
    }

    public CookingFrontOfHouseRestoreResult RestoreCheckpoint(
        CookingFrontOfHouseCheckpoint checkpoint,
        CookingRecipeSimulation kitchen)
    {
        ArgumentNullException.ThrowIfNull(kitchen);
        if (checkpoint?.State is null)
            return CookingFrontOfHouseRestoreResult.Reject(CookingFrontOfHouseRestoreReason.CheckpointNull);
        if (!Equals(_schedule, checkpoint.State.Schedule))
            return CookingFrontOfHouseRestoreResult.Reject(CookingFrontOfHouseRestoreReason.ScheduleMismatch);
        if (_companion != checkpoint.State.Companion.Id)
            return CookingFrontOfHouseRestoreResult.Reject(CookingFrontOfHouseRestoreReason.CompanionMismatch);

        var restored = Restore(checkpoint, kitchen);
        if (!restored.Accepted || restored.FrontOfHouse is null)
            return restored;
        CopyFrom(restored.FrontOfHouse);
        return CookingFrontOfHouseRestoreResult.Accept(this, checkpoint.ActiveOrderTemplate);
    }

    public static CookingFrontOfHouseRestoreResult Restore(
        CookingFrontOfHouseCheckpoint checkpoint,
        CookingRecipeSimulation kitchen)
    {
        ArgumentNullException.ThrowIfNull(kitchen);
        if (checkpoint?.State is null)
            return CookingFrontOfHouseRestoreResult.Reject(CookingFrontOfHouseRestoreReason.CheckpointNull);

        var reason = ValidateCheckpoint(checkpoint, kitchen);
        if (reason != CookingFrontOfHouseRestoreReason.None)
            return CookingFrontOfHouseRestoreResult.Reject(reason);

        var state = checkpoint.State;
        var house = new CookingFrontOfHouse(state.Schedule, state.Companion.Id);
        house.Apply(state);
        return CookingFrontOfHouseRestoreResult.Accept(house, checkpoint.ActiveOrderTemplate);
    }

    /// <summary>运行帧走一步。暂停或厨房已关闭时调用方不要进来。</summary>
    public CookingFrontOfHouseStep Step(CookingRecipeSimulation kitchen, OrderTemplateId template)
        => Step(kitchen, CookingFrontOfHouseMenu.Single(template));

    public CookingFrontOfHouseStep Step(CookingRecipeSimulation kitchen, CookingFrontOfHouseMenu menu)
    {
        ArgumentNullException.ThrowIfNull(kitchen);
        BindMenu(menu);
        NoticeDirtyBowls(kitchen);
        AdvanceGuests(kitchen);
        if (_work.Kind != CookingCompanionWorkKind.Idle)
            AdvanceCompanion(kitchen);
        if (_work.Kind == CookingCompanionWorkKind.Idle)
            StartNextJob(kitchen);
        if (!_closing)
            TrySeatGuest();
        if (!_closing && _serviceTicks >= _schedule.ServiceTicks)
            _closing = true;
        return new CookingFrontOfHouseStep(SeatedCount, _closing, CanSucceed, _unsatisfied.Count);
    }

    /// <summary>失败重开：丢掉座位、未满足、营业时钟和洗碗队列。不给新厨房开单，也不洗旧碗。</summary>
    public void DropFailedScene()
    {
        foreach (var table in _tables)
            table.Reset();
        _unsatisfied.Clear();
        _washQueue.Clear();
        _queuedBowls.Clear();
        _serviceTicks = 0;
        _ticksUntilNextGuest = 0;
        _closing = false;
        _nextGuest = 0;
        _work = CookingCompanionWork.Idle();
        _completedCompanionTasks = 0;
    }

    /// <summary>下一小关：先收完正在做的询问或洗碗，再清掉座位、未满足和营业时钟。厨房里的脏碗保留。</summary>
    public void ResetForNextLevel(CookingRecipeSimulation kitchen, OrderTemplateId template)
        => ResetForNextLevel(kitchen, CookingFrontOfHouseMenu.Single(template));

    public void ResetForNextLevel(CookingRecipeSimulation kitchen, CookingFrontOfHouseMenu menu)
    {
        FinishInProgress(kitchen, menu);
        foreach (var table in _tables)
            table.Reset();
        _unsatisfied.Clear();
        _serviceTicks = 0;
        _ticksUntilNextGuest = 0;
        _closing = false;
        _nextGuest = 0;
        _work = CookingCompanionWork.Idle();
        _completedCompanionTasks = 0;
    }

    /// <summary>成功收口前把做到一半的询问和洗碗按完成处理。</summary>
    public void FinishInProgress(CookingRecipeSimulation kitchen, OrderTemplateId template)
        => FinishInProgress(kitchen, CookingFrontOfHouseMenu.Single(template));

    public void FinishInProgress(CookingRecipeSimulation kitchen, CookingFrontOfHouseMenu menu)
    {
        ArgumentNullException.ThrowIfNull(kitchen);
        BindMenu(menu);
        NoticeDirtyBowls(kitchen);
        foreach (var table in _tables.Where(table => table.Customer is { Order: null }).ToArray())
            CompleteInquiry(kitchen, table);

        if (_work.Kind == CookingCompanionWorkKind.Washing && _work.Bowl is { } finishingBowl)
            CompleteWash(kitchen, finishingBowl);
        _work = CookingCompanionWork.Idle();
    }

    private void AdvanceCompanion(CookingRecipeSimulation kitchen)
    {
        if (_work.Kind == CookingCompanionWorkKind.Idle)
            return;

        _work.Elapsed++;
        if (_work.Elapsed < _work.RequiredTicks)
            return;

        if (_work.Kind == CookingCompanionWorkKind.Inquiring &&
            FindTable(_work.TargetTable, _work.TargetCustomer) is { } table)
        {
            CompleteInquiry(kitchen, table);
        }
        else if (_work.Bowl is { } bowl)
        {
            CompleteWash(kitchen, bowl);
        }
        _work = CookingCompanionWork.Idle();
    }

    private void CompleteInquiry(CookingRecipeSimulation kitchen, CookingTable table)
    {
        if (table.Customer is not { } customer)
            return;

        var template = TemplateFor(customer.ArrivalOrder);
        var order = CustomerOrder(customer.Id);
        var opened = kitchen.OpenOrder(order, template);
        if (!opened.Accepted)
            return;

        customer.Phase = CookingTablePhase.Ordered;
        customer.Order = order;
        customer.OrderTemplate = template;
        customer.ElapsedTicks = 0;
        _completedCompanionTasks++;
    }

    private void CompleteWash(CookingRecipeSimulation kitchen, ItemId bowl)
    {
        var washed = kitchen.CompleteWash(bowl);
        if (!washed.Accepted)
        {
            _washQueue.Enqueue(bowl);
            return;
        }

        _queuedBowls.Remove(bowl);
        _completedCompanionTasks++;
    }

    private void StartNextJob(CookingRecipeSimulation kitchen)
    {
        var waiting = _tables
            .Where(table => table.Customer?.Phase == CookingTablePhase.WaitingForInquiry)
            .OrderBy(table => table.Customer!.ArrivalOrder)
            .FirstOrDefault();
        if (waiting?.Customer is { } customer)
        {
            customer.Phase = CookingTablePhase.InquiryInProgress;
            _work = CookingCompanionWork.Inquiry(customer.Id, waiting.Id, _schedule.InquiryTicks);
            AdvanceCompanion(kitchen);
            return;
        }

        while (_washQueue.Count > 0)
        {
            var bowl = _washQueue.Dequeue();
            if (!kitchen.DirtyBowlsAwaitingWash().Contains(bowl))
            {
                _queuedBowls.Remove(bowl);
                continue;
            }

            _work = CookingCompanionWork.Wash(bowl, RequiredWashTicks());
            AdvanceCompanion(kitchen);
            return;
        }
    }

    private void AdvanceGuests(CookingRecipeSimulation kitchen)
    {
        foreach (var table in _tables)
        {
            if (table.Customer is not { } customer)
                continue;

            customer.ElapsedTicks++;
            if (customer.Phase is CookingTablePhase.InquiryInProgress &&
                customer.ElapsedTicks < _schedule.WaitLimitTicks)
            {
                continue;
            }
            if (customer.Phase == CookingTablePhase.Dining)
            {
                if (customer.ElapsedTicks >= _schedule.DiningTicks)
                    table.Reset();
                continue;
            }

            if (customer.Order is { } served)
            {
                var status = OrderStatus(kitchen, served);
                if (status == CookingOrderStatus.Completed.ToString())
                {
                    customer.Phase = CookingTablePhase.Dining;
                    customer.ElapsedTicks = 0;
                    continue;
                }

                if (status == CookingOrderStatus.Unsatisfied.ToString())
                {
                    CancelInquiry(customer.Id);
                    table.Reset();
                    continue;
                }
            }

            if (customer.ElapsedTicks < _schedule.WaitLimitTicks)
                continue;

            var unsatisfied = customer.Order ?? CustomerOrder(customer.Id);
            if (customer.Order is { } waitingOrder)
            {
                var marked = kitchen.MarkOrderUnsatisfied(waitingOrder);
                if (marked.Accepted)
                    _unsatisfied.Add(waitingOrder);
            }
            else
            {
                _unsatisfied.Add(unsatisfied);
            }

            CancelInquiry(customer.Id);
            table.Reset();
        }
    }

    private static string? OrderStatus(CookingRecipeSimulation kitchen, OrderId order) =>
        kitchen.Orders.FirstOrDefault(candidate => candidate.Id == order)?.Status;

    private void TrySeatGuest()
    {
        if (!_closing)
            _serviceTicks++;
        if (_ticksUntilNextGuest > 0)
        {
            _ticksUntilNextGuest--;
            return;
        }

        _ticksUntilNextGuest = _schedule.ArrivalIntervalTicks - 1;
        var table = _tables.FirstOrDefault(candidate => candidate.Customer is null);
        if (table is null)
            return;

        var sequence = ++_nextGuest;
        table.Customer = new CookingCustomer(new CookingCustomerId($"customer-{sequence}"), sequence,
            CookingTablePhase.WaitingForInquiry, 0, null);
    }

    private void NoticeDirtyBowls(CookingRecipeSimulation kitchen)
    {
        foreach (var bowl in kitchen.DirtyBowlsAwaitingWash())
        {
            if (_queuedBowls.Add(bowl))
                _washQueue.Enqueue(bowl);
        }
    }

    private CookingTable? FindTable(string? tableId, CookingCustomerId? customerId)
    {
        if (tableId is null || customerId is null)
            return null;
        return _tables.FirstOrDefault(table =>
            StringComparer.Ordinal.Equals(table.Id, tableId) && table.Customer?.Id == customerId);
    }

    private void CancelInquiry(CookingCustomerId customer)
    {
        if (_work.Kind == CookingCompanionWorkKind.Inquiring && _work.TargetCustomer == customer)
            _work = CookingCompanionWork.Idle();
    }

    private void CopyFrom(CookingFrontOfHouse source)
    {
        _tables.Clear();
        foreach (var table in source._tables)
            _tables.Add(table.Copy());
        _washQueue.Clear();
        foreach (var bowl in source._washQueue)
            _washQueue.Enqueue(bowl);
        _queuedBowls.Clear();
        foreach (var bowl in source._queuedBowls)
            _queuedBowls.Add(bowl);
        _unsatisfied.Clear();
        _unsatisfied.AddRange(source._unsatisfied);
        _work = source._work;
        _serviceTicks = source._serviceTicks;
        _ticksUntilNextGuest = source._ticksUntilNextGuest;
        _closing = source._closing;
        _nextGuest = source._nextGuest;
        _completedCompanionTasks = source._completedCompanionTasks;
        _orderMenu = source._orderMenu.ToArray();
    }

    private void Apply(CookingFrontOfHouseSnapshot state)
    {
        foreach (var table in _tables)
            table.Reset();
        foreach (var customer in state.Customers)
        {
            var table = _tables.Single(candidate => StringComparer.Ordinal.Equals(candidate.Id, customer.TableId));
            table.Customer = CookingCustomer.From(customer);
        }

        _washQueue.Clear();
        foreach (var bowl in state.WashQueue)
            _washQueue.Enqueue(bowl);
        _queuedBowls.Clear();
        foreach (var bowl in state.WashQueue)
            _queuedBowls.Add(bowl);
        if (state.Companion.Bowl is { } activeBowl)
            _queuedBowls.Add(activeBowl);
        _unsatisfied.Clear();
        _unsatisfied.AddRange(state.UnsatisfiedOrders);
        _work = CookingCompanionWork.From(state.Companion);
        _serviceTicks = state.ServiceTicks;
        _ticksUntilNextGuest = state.TicksUntilNextGuest;
        _closing = state.Closing;
        _nextGuest = state.NextCustomerSequence;
        _completedCompanionTasks = state.Companion.CompletedTaskCount;
        _orderMenu = state.OrderMenu.ToArray();
    }

    private static CookingFrontOfHouseRestoreReason ValidateCheckpoint(
        CookingFrontOfHouseCheckpoint checkpoint,
        CookingRecipeSimulation kitchen)
    {
        if (string.IsNullOrWhiteSpace(checkpoint.ActiveOrderTemplate.Value))
            return CookingFrontOfHouseRestoreReason.IdentityInvalid;
        var state = checkpoint.State;
        if (state.OrderMenu is null || state.OrderMenu.Count == 0 ||
            state.OrderMenu.Any(template => string.IsNullOrWhiteSpace(template.Value)) ||
            state.OrderMenu.Distinct().Count() != state.OrderMenu.Count ||
            state.OrderMenu.Any(template => !kitchen.HasOrderTemplate(template)) ||
            checkpoint.ActiveOrderTemplate != state.OrderMenu[0])
            return CookingFrontOfHouseRestoreReason.IdentityInvalid;
        try
        {
            ValidateSchedule(state.Schedule);
        }
        catch (ArgumentException)
        {
            return CookingFrontOfHouseRestoreReason.ScheduleInvalid;
        }

        if (state.Customers is null || state.WashQueue is null || state.UnsatisfiedOrders is null ||
            state.Companion is null || string.IsNullOrWhiteSpace(state.Companion.Id.Value))
        {
            return CookingFrontOfHouseRestoreReason.CheckpointNull;
        }
        if (state.ServiceTicks < 0 || state.ServiceTicks > state.Schedule.ServiceTicks ||
            state.TicksUntilNextGuest < 0 || state.NextCustomerSequence < 0 ||
            state.TicksUntilNextGuest >= state.Schedule.ArrivalIntervalTicks ||
            state.Closing != (state.ServiceTicks >= state.Schedule.ServiceTicks))
        {
            return CookingFrontOfHouseRestoreReason.CounterInvalid;
        }
        if (state.Customers.Count > state.Schedule.TableCount)
            return CookingFrontOfHouseRestoreReason.TableInvalid;

        var expectedTables = Enumerable.Range(1, state.Schedule.TableCount)
            .Select(index => $"table-{index}")
            .ToHashSet(StringComparer.Ordinal);
        var tableIds = new HashSet<string>(StringComparer.Ordinal);
        var customerIds = new HashSet<CookingCustomerId>();
        var arrivalOrders = new HashSet<int>();
        foreach (var customer in state.Customers)
        {
            if (!expectedTables.Contains(customer.TableId) || !tableIds.Add(customer.TableId))
                return CookingFrontOfHouseRestoreReason.TableInvalid;
            if (string.IsNullOrWhiteSpace(customer.Id.Value) || !customerIds.Add(customer.Id) ||
                customer.ArrivalOrder < 1 || customer.ArrivalOrder > state.NextCustomerSequence ||
                !arrivalOrders.Add(customer.ArrivalOrder) || customer.ElapsedTicks < 0 ||
                !StringComparer.Ordinal.Equals(customer.Id.Value, $"customer-{customer.ArrivalOrder}"))
            {
                return CookingFrontOfHouseRestoreReason.CustomerInvalid;
            }

            var expectedOrder = CustomerOrder(customer.Id);
            var order = customer.Order is { } orderId
                ? kitchen.Orders.FirstOrDefault(candidate => candidate.Id == orderId)
                : null;
            switch (customer.Phase)
            {
                case CookingTablePhase.WaitingForInquiry:
                case CookingTablePhase.InquiryInProgress:
                    if (customer.Order is not null || customer.OrderTemplate is not null ||
                        customer.ElapsedTicks >= state.Schedule.WaitLimitTicks)
                        return CookingFrontOfHouseRestoreReason.OrderInvalid;
                    break;
                case CookingTablePhase.Ordered:
                    var expectedTemplate = state.OrderMenu[(customer.ArrivalOrder - 1) % state.OrderMenu.Count];
                    if (customer.Order != expectedOrder || order is null ||
                        customer.OrderTemplate != expectedTemplate || order.Template != expectedTemplate ||
                        order.Status is not (nameof(CookingOrderStatus.Open) or nameof(CookingOrderStatus.Completed)) ||
                        customer.ElapsedTicks >= state.Schedule.WaitLimitTicks)
                    {
                        return CookingFrontOfHouseRestoreReason.OrderInvalid;
                    }
                    break;
                case CookingTablePhase.Dining:
                    var diningTemplate = state.OrderMenu[(customer.ArrivalOrder - 1) % state.OrderMenu.Count];
                    if (customer.Order != expectedOrder || customer.OrderTemplate != diningTemplate ||
                        order?.Template != diningTemplate || order.Status != nameof(CookingOrderStatus.Completed) ||
                        customer.ElapsedTicks >= state.Schedule.DiningTicks)
                        return CookingFrontOfHouseRestoreReason.OrderInvalid;
                    break;
                default:
                    return CookingFrontOfHouseRestoreReason.CustomerInvalid;
            }
        }

        var companion = state.Companion;
        var expectedUnlocked = companion.CompletedTaskCount >= state.Schedule.CompanionGrowthTaskThreshold;
        if (companion.ElapsedTicks < 0 || companion.RequiredTicks < 0 || companion.CompletedTaskCount < 0 ||
            companion.WashSpeedUnlocked != expectedUnlocked)
            return CookingFrontOfHouseRestoreReason.CompanionInvalid;
        switch (companion.Work)
        {
            case CookingCompanionWorkKind.Idle:
                if (companion.TargetCustomer is not null || companion.TargetTable is not null || companion.Bowl is not null ||
                    companion.ElapsedTicks != 0 || companion.RequiredTicks != 0)
                {
                    return CookingFrontOfHouseRestoreReason.CompanionInvalid;
                }
                break;
            case CookingCompanionWorkKind.Inquiring:
                var target = state.Customers.FirstOrDefault(customer =>
                    customer.Id == companion.TargetCustomer &&
                    StringComparer.Ordinal.Equals(customer.TableId, companion.TargetTable));
                if (target is null || target.Phase != CookingTablePhase.InquiryInProgress || companion.Bowl is not null ||
                    companion.RequiredTicks != state.Schedule.InquiryTicks || companion.ElapsedTicks < 1 ||
                    companion.ElapsedTicks >= companion.RequiredTicks)
                {
                    return CookingFrontOfHouseRestoreReason.CompanionInvalid;
                }
                break;
            case CookingCompanionWorkKind.Washing:
                var expectedWashTicks = RequiredWashTicks(state.Schedule, companion.WashSpeedUnlocked);
                if (companion.TargetCustomer is not null || companion.TargetTable is not null || companion.Bowl is null ||
                    companion.RequiredTicks != expectedWashTicks || companion.ElapsedTicks < 1 ||
                    companion.ElapsedTicks >= companion.RequiredTicks ||
                    !kitchen.DirtyBowlsAwaitingWash().Contains(companion.Bowl.Value))
                {
                    return CookingFrontOfHouseRestoreReason.CompanionInvalid;
                }
                break;
            default:
                return CookingFrontOfHouseRestoreReason.CompanionInvalid;
        }

        if (state.Customers.Any(customer => customer.Phase == CookingTablePhase.InquiryInProgress &&
            customer.Id != companion.TargetCustomer))
        {
            return CookingFrontOfHouseRestoreReason.CompanionInvalid;
        }

        var dirty = kitchen.DirtyBowlsAwaitingWash().ToHashSet();
        var queue = new HashSet<ItemId>();
        foreach (var bowl in state.WashQueue)
        {
            if (!queue.Add(bowl) || !dirty.Contains(bowl) || companion.Bowl == bowl)
                return CookingFrontOfHouseRestoreReason.WashQueueInvalid;
        }

        var unsatisfied = new HashSet<OrderId>();
        foreach (var order in state.UnsatisfiedOrders)
        {
            if (!TryGetCustomerSequence(order, out var sequence) ||
                sequence > state.NextCustomerSequence ||
                state.Customers.Any(customer => customer.Id.Value == $"customer-{sequence}") ||
                !unsatisfied.Add(order))
            {
                return CookingFrontOfHouseRestoreReason.UnsatisfiedInvalid;
            }
            var kitchenOrder = kitchen.Orders.FirstOrDefault(candidate => candidate.Id == order);
            if (kitchenOrder is not null && kitchenOrder.Status != nameof(CookingOrderStatus.Unsatisfied))
                return CookingFrontOfHouseRestoreReason.UnsatisfiedInvalid;
        }

        return CookingFrontOfHouseRestoreReason.None;
    }

    private static void ValidateSchedule(CookingFrontOfHouseSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (schedule.TableCount < 1)
            throw new ArgumentOutOfRangeException(nameof(schedule), "At least one table is required.");
        if (schedule.ServiceTicks < 1 || schedule.ArrivalIntervalTicks < 1 || schedule.InquiryTicks < 1 ||
            schedule.WashTicks < 1 || schedule.DiningTicks < 1 || schedule.WaitLimitTicks < 1 ||
            schedule.CompanionGrowthTaskThreshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schedule),
                "Front-of-house timings and the companion growth threshold must be positive.");
        }
    }

    private static OrderId CustomerOrder(CookingCustomerId customer) => new($"{customer.Value}-order");

    private void BindMenu(CookingFrontOfHouseMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (_orderMenu.Length == 0)
        {
            _orderMenu = menu.Templates.ToArray();
            return;
        }
        if (!_orderMenu.SequenceEqual(menu.Templates))
            throw new InvalidOperationException("The front-of-house menu cannot change during a Level.");
    }

    private OrderTemplateId TemplateFor(int arrivalOrder)
    {
        if (_orderMenu.Length == 0)
            throw new InvalidOperationException("The front-of-house menu has not been configured.");
        return _orderMenu[(arrivalOrder - 1) % _orderMenu.Length];
    }

    private bool WashSpeedUnlocked =>
        _completedCompanionTasks >= _schedule.CompanionGrowthTaskThreshold;

    private int RequiredWashTicks() => RequiredWashTicks(_schedule, WashSpeedUnlocked);

    private static int RequiredWashTicks(CookingFrontOfHouseSchedule schedule, bool unlocked) =>
        unlocked ? Math.Max(1, schedule.WashTicks / 2 + schedule.WashTicks % 2) : schedule.WashTicks;

    private static bool TryGetCustomerSequence(OrderId order, out int sequence)
    {
        const string prefix = "customer-";
        const string suffix = "-order";
        sequence = 0;
        var value = order.Value;
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(prefix, StringComparison.Ordinal) ||
            !value.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var length = value.Length - prefix.Length - suffix.Length;
        return length > 0 &&
               int.TryParse(value.AsSpan(prefix.Length, length), out sequence) &&
               sequence > 0 &&
               StringComparer.Ordinal.Equals(value, $"customer-{sequence}-order");
    }

    private sealed class CookingTable
    {
        public CookingTable(string id) => Id = id;

        public string Id { get; }

        public CookingCustomer? Customer { get; set; }

        public void Reset() => Customer = null;

        public CookingTable Copy() => new(Id) { Customer = Customer?.Copy() };
    }

    private sealed class CookingCustomer
    {
        public CookingCustomer(CookingCustomerId id, int arrivalOrder, CookingTablePhase phase, int elapsedTicks,
            OrderId? order, OrderTemplateId? orderTemplate = null)
        {
            Id = id;
            ArrivalOrder = arrivalOrder;
            Phase = phase;
            ElapsedTicks = elapsedTicks;
            Order = order;
            OrderTemplate = orderTemplate;
        }

        public CookingCustomerId Id { get; }
        public int ArrivalOrder { get; }
        public CookingTablePhase Phase { get; set; }
        public int ElapsedTicks { get; set; }
        public OrderId? Order { get; set; }
        public OrderTemplateId? OrderTemplate { get; set; }

        public CookingCustomerSnapshot Snapshot(string tableId) =>
            new(Id, tableId, ArrivalOrder, Phase, ElapsedTicks, Order, OrderTemplate);

        public CookingCustomer Copy() => new(Id, ArrivalOrder, Phase, ElapsedTicks, Order, OrderTemplate);

        public static CookingCustomer From(CookingCustomerSnapshot snapshot) =>
            new(snapshot.Id, snapshot.ArrivalOrder, snapshot.Phase, snapshot.ElapsedTicks, snapshot.Order,
                snapshot.OrderTemplate);
    }

    private struct CookingCompanionWork
    {
        public CookingCompanionWorkKind Kind { get; private set; }
        public CookingCustomerId? TargetCustomer { get; private set; }
        public string? TargetTable { get; private set; }
        public ItemId? Bowl { get; private set; }
        public int Elapsed { get; set; }
        public int RequiredTicks { get; private set; }

        public static CookingCompanionWork Idle() => new() { Kind = CookingCompanionWorkKind.Idle };

        public static CookingCompanionWork Inquiry(CookingCustomerId customer, string table, int requiredTicks) =>
            new()
            {
                Kind = CookingCompanionWorkKind.Inquiring,
                TargetCustomer = customer,
                TargetTable = table,
                RequiredTicks = requiredTicks,
            };

        public static CookingCompanionWork Wash(ItemId bowl, int requiredTicks) =>
            new() { Kind = CookingCompanionWorkKind.Washing, Bowl = bowl, RequiredTicks = requiredTicks };

        public static CookingCompanionWork From(CookingCompanionSnapshot snapshot) => new()
        {
            Kind = snapshot.Work,
            TargetCustomer = snapshot.TargetCustomer,
            TargetTable = snapshot.TargetTable,
            Bowl = snapshot.Bowl,
            Elapsed = snapshot.ElapsedTicks,
            RequiredTicks = snapshot.RequiredTicks,
        };
    }
}

public sealed record CookingFrontOfHouseSchedule(
    int TableCount,
    int ServiceTicks,
    int ArrivalIntervalTicks,
    int InquiryTicks,
    int WashTicks,
    int DiningTicks,
    int WaitLimitTicks,
    int CompanionGrowthTaskThreshold = 3);

public sealed record CookingFrontOfHouseStep(int SeatedCount, bool Closing, bool CanSucceed, int UnsatisfiedCount);

public sealed record CookingCustomerSnapshot(
    CookingCustomerId Id,
    string TableId,
    int ArrivalOrder,
    CookingTablePhase Phase,
    int ElapsedTicks,
    OrderId? Order,
    OrderTemplateId? OrderTemplate = null);

public sealed record CookingCompanionSnapshot(
    CookingCompanionId Id,
    CookingCompanionWorkKind Work,
    CookingCustomerId? TargetCustomer,
    string? TargetTable,
    ItemId? Bowl,
    int ElapsedTicks,
    int RequiredTicks,
    int CompletedTaskCount,
    bool WashSpeedUnlocked);

public sealed record CookingFrontOfHouseSnapshot(
    CookingFrontOfHouseSchedule Schedule,
    int ServiceTicks,
    int TicksUntilNextGuest,
    bool Closing,
    int NextCustomerSequence,
    IReadOnlyList<OrderTemplateId> OrderMenu,
    IReadOnlyList<CookingCustomerSnapshot> Customers,
    CookingCompanionSnapshot Companion,
    IReadOnlyList<ItemId> WashQueue,
    IReadOnlyList<OrderId> UnsatisfiedOrders)
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string CanonicalText() => JsonSerializer.Serialize(new CanonicalFrontOfHouse(
        Schedule.TableCount,
        Schedule.ServiceTicks,
        Schedule.ArrivalIntervalTicks,
        Schedule.InquiryTicks,
        Schedule.WashTicks,
        Schedule.DiningTicks,
        Schedule.WaitLimitTicks,
        Schedule.CompanionGrowthTaskThreshold,
        ServiceTicks,
        TicksUntilNextGuest,
        Closing,
        NextCustomerSequence,
        OrderMenu.Select(template => template.Value).ToArray(),
        Customers
            .OrderBy(customer => customer.TableId, StringComparer.Ordinal)
            .ThenBy(customer => customer.Id.Value, StringComparer.Ordinal)
            .Select(customer => new CanonicalCustomer(customer.Id.Value, customer.TableId, customer.ArrivalOrder,
                customer.Phase.ToString(), customer.ElapsedTicks, customer.Order?.Value,
                customer.OrderTemplate?.Value))
            .ToArray(),
        new CanonicalCompanion(Companion.Id.Value, Companion.Work.ToString(), Companion.TargetCustomer?.Value,
            Companion.TargetTable, Companion.Bowl?.Value, Companion.ElapsedTicks, Companion.RequiredTicks,
            Companion.CompletedTaskCount, Companion.WashSpeedUnlocked),
        WashQueue.Select(bowl => bowl.Value).ToArray(),
        UnsatisfiedOrders.Select(order => order.Value).ToArray()), CanonicalJsonOptions);

    public string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));

    private sealed record CanonicalFrontOfHouse(
        int TableCount,
        int ScheduleServiceTicks,
        int ArrivalIntervalTicks,
        int InquiryTicks,
        int WashTicks,
        int DiningTicks,
        int WaitLimitTicks,
        int CompanionGrowthTaskThreshold,
        int ServiceTicks,
        int TicksUntilNextGuest,
        bool Closing,
        int NextCustomerSequence,
        IReadOnlyList<string> OrderMenu,
        IReadOnlyList<CanonicalCustomer> Customers,
        CanonicalCompanion Companion,
        IReadOnlyList<string> WashQueue,
        IReadOnlyList<string> UnsatisfiedOrders);

    private sealed record CanonicalCustomer(
        string Id,
        string TableId,
        int ArrivalOrder,
        string Phase,
        int ElapsedTicks,
        string? OrderId,
        string? OrderTemplateId);

    private sealed record CanonicalCompanion(
        string Id,
        string Work,
        string? TargetCustomerId,
        string? TargetTableId,
        string? BowlId,
        int ElapsedTicks,
        int RequiredTicks,
        int CompletedTaskCount,
        bool WashSpeedUnlocked);
}

public sealed record CookingFrontOfHouseCheckpoint(
    OrderTemplateId ActiveOrderTemplate,
    CookingFrontOfHouseSnapshot State)
{
    public string CanonicalText() => JsonSerializer.Serialize(new CanonicalCheckpoint(
        ActiveOrderTemplate.Value,
        State.CanonicalText()));

    public string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));

    private sealed record CanonicalCheckpoint(string ActiveOrderTemplate, string StateCanonical);
}

public enum CookingFrontOfHouseRestoreReason
{
    None,
    CheckpointNull,
    ScheduleInvalid,
    ScheduleMismatch,
    CompanionMismatch,
    IdentityInvalid,
    CounterInvalid,
    TableInvalid,
    CustomerInvalid,
    OrderInvalid,
    CompanionInvalid,
    WashQueueInvalid,
    UnsatisfiedInvalid,
}

public sealed record CookingFrontOfHouseRestoreResult(
    bool Accepted,
    CookingFrontOfHouseRestoreReason Reason,
    CookingFrontOfHouse? FrontOfHouse = null,
    OrderTemplateId? ActiveOrderTemplate = null)
{
    public static CookingFrontOfHouseRestoreResult Accept(
        CookingFrontOfHouse frontOfHouse,
        OrderTemplateId activeOrderTemplate) =>
        new(true, CookingFrontOfHouseRestoreReason.None, frontOfHouse, activeOrderTemplate);

    public static CookingFrontOfHouseRestoreResult Reject(CookingFrontOfHouseRestoreReason reason) =>
        new(false, reason);
}

public enum CookingTablePhase
{
    Empty,
    WaitingForInquiry,
    InquiryInProgress,
    Ordered,
    Dining,
}

public enum CookingCompanionWorkKind
{
    Idle,
    Inquiring,
    Washing,
}
