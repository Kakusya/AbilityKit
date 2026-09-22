namespace AbilityKit.Game.Cooking;

/// <summary>
/// 一位固定伙伴的前厅节奏：问完才开单，没有询问才洗碗。
/// 收益、评价、伙伴成长和失败条件不在这里。
/// </summary>
public sealed class CookingFrontOfHouse
{
    private readonly CookingFrontOfHouseSchedule _schedule;
    private readonly List<CookingTable> _tables = new();
    private readonly Queue<ItemId> _washQueue = new();
    private readonly HashSet<ItemId> _queuedBowls = new();
    private readonly List<OrderId> _unsatisfied = new();
    private CookingCompanionWork _work = CookingCompanionWork.Idle();
    private int _serviceTicks;
    private int _ticksUntilNextGuest;
    private bool _closing;
    private int _nextGuest;

    public CookingFrontOfHouse(CookingFrontOfHouseSchedule schedule)
    {
        if (schedule.TableCount < 1)
            throw new ArgumentOutOfRangeException(nameof(schedule), "At least one table is required.");
        if (schedule.ServiceTicks < 1 || schedule.ArrivalIntervalTicks < 1 || schedule.InquiryTicks < 1 ||
            schedule.WashTicks < 1 || schedule.DiningTicks < 1 || schedule.WaitLimitTicks < 1)
            throw new ArgumentOutOfRangeException(nameof(schedule), "Front-of-house timings must be positive.");

        _schedule = schedule;
        _ticksUntilNextGuest = 0;
        for (var index = 1; index <= schedule.TableCount; index++)
            _tables.Add(new CookingTable($"table-{index}"));
    }

    public bool IsClosing => _closing;

    public bool CompanionIdle => _work.Kind == CookingCompanionWorkKind.Idle;

    public IReadOnlyList<OrderId> UnsatisfiedOrders => _unsatisfied;

    public int SeatedCount => _tables.Count(table => table.Phase != CookingTablePhase.Empty);

    public bool CanSucceed =>
        _closing && SeatedCount == 0 && _work.Kind == CookingCompanionWorkKind.Idle;

    /// <summary>运行帧走一步。暂停或厨房已关闭时调用方不要进来。</summary>
    public CookingFrontOfHouseStep Step(CookingRecipeSimulation kitchen, OrderTemplateId template)
    {
        ArgumentNullException.ThrowIfNull(kitchen);
        NoticeDirtyBowls(kitchen);
        AdvanceGuests(kitchen);
        if (_work.Kind != CookingCompanionWorkKind.Idle)
            AdvanceCompanion(kitchen, template);
        if (_work.Kind == CookingCompanionWorkKind.Idle)
            StartNextJob(kitchen, template);
        if (!_closing)
            TrySeatGuest();
        if (!_closing && _serviceTicks >= _schedule.ServiceTicks)
            _closing = true;
        return new CookingFrontOfHouseStep(SeatedCount, _closing, CanSucceed, _unsatisfied.Count);
    }

    /// <summary>成功收口前把做到一半的询问和洗碗按完成处理。</summary>
    public void FinishInProgress(CookingRecipeSimulation kitchen, OrderTemplateId template)
    {
        ArgumentNullException.ThrowIfNull(kitchen);
        NoticeDirtyBowls(kitchen);
        if (_work.Kind == CookingCompanionWorkKind.Inquiring && _work.Table is not null)
            CompleteInquiry(kitchen, template, _work.Table);
        else if (_work.Kind == CookingCompanionWorkKind.Washing && _work.Bowl is { } finishingBowl)
            CompleteWash(kitchen, finishingBowl);
        _work = CookingCompanionWork.Idle();
    }

    private void AdvanceCompanion(CookingRecipeSimulation kitchen, OrderTemplateId template)
    {
        if (_work.Kind == CookingCompanionWorkKind.Idle)
            return;

        _work.Elapsed++;
        var required = _work.Kind == CookingCompanionWorkKind.Inquiring
            ? _schedule.InquiryTicks
            : _schedule.WashTicks;
        if (_work.Elapsed < required)
            return;

        if (_work.Kind == CookingCompanionWorkKind.Inquiring && _work.Table is not null)
            CompleteInquiry(kitchen, template, _work.Table);
        else if (_work.Bowl is { } bowl)
            CompleteWash(kitchen, bowl);
        _work = CookingCompanionWork.Idle();
    }

    private void CompleteInquiry(CookingRecipeSimulation kitchen, OrderTemplateId template, CookingTable table)
    {
        var order = new OrderId($"{table.Id}-order");
        var opened = kitchen.OpenOrder(order, template);
        if (!opened.Accepted)
            return;

        table.Phase = CookingTablePhase.Ordered;
        table.Order = order;
        table.TicksSeated = 0;
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
    }

    private void StartNextJob(CookingRecipeSimulation kitchen, OrderTemplateId template)
    {
        var waiting = _tables
            .Where(table => table.Phase == CookingTablePhase.WaitingForInquiry)
            .OrderBy(table => table.ArrivalOrder)
            .FirstOrDefault();
        if (waiting is not null)
        {
            waiting.Phase = CookingTablePhase.InquiryInProgress;
            _work = CookingCompanionWork.Inquiry(waiting);
            AdvanceCompanion(kitchen, template);
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

            _work = CookingCompanionWork.Wash(bowl);
            AdvanceCompanion(kitchen, template);
            return;
        }
    }

    private void AdvanceGuests(CookingRecipeSimulation kitchen)
    {
        foreach (var table in _tables)
        {
            if (table.Phase is CookingTablePhase.Empty)
                continue;

            table.TicksSeated++;
            if (table.Phase is CookingTablePhase.InquiryInProgress && table.TicksSeated < _schedule.WaitLimitTicks)
                continue;
            if (table.Phase == CookingTablePhase.Dining)
            {
                if (table.TicksSeated >= _schedule.DiningTicks)
                    table.Reset();
                continue;
            }

            if (table.Order is { } order && OrderClosed(kitchen, order))
            {
                table.Reset();
                continue;
            }

            if (table.TicksSeated < _schedule.WaitLimitTicks)
                continue;

            if (table.Order is { } waitingOrder)
            {
                var marked = kitchen.MarkOrderUnsatisfied(waitingOrder);
                if (marked.Accepted)
                    _unsatisfied.Add(waitingOrder);
            }
            else
            {
                var missed = new OrderId($"{table.Id}-order");
                _unsatisfied.Add(missed);
            }

            table.Reset();
        }
    }

    private static bool OrderClosed(CookingRecipeSimulation kitchen, OrderId order) =>
        kitchen.Orders.Any(candidate => candidate.Id == order && candidate.Status != CookingOrderStatus.Open.ToString());

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
        var table = _tables.FirstOrDefault(candidate => candidate.Phase == CookingTablePhase.Empty);
        if (table is null)
            return;

        table.Phase = CookingTablePhase.WaitingForInquiry;
        table.ArrivalOrder = ++_nextGuest;
        table.TicksSeated = 0;
    }

    private void NoticeDirtyBowls(CookingRecipeSimulation kitchen)
    {
        foreach (var bowl in kitchen.DirtyBowlsAwaitingWash())
        {
            if (_queuedBowls.Add(bowl))
                _washQueue.Enqueue(bowl);
        }
    }

    private sealed class CookingTable
    {
        public CookingTable(string id) => Id = id;

        public string Id { get; }

        public CookingTablePhase Phase { get; set; } = CookingTablePhase.Empty;

        public int ArrivalOrder { get; set; }

        public int TicksSeated { get; set; }

        public OrderId? Order { get; set; }

        public void Reset()
        {
            Phase = CookingTablePhase.Empty;
            ArrivalOrder = 0;
            TicksSeated = 0;
            Order = null;
        }
    }

    private struct CookingCompanionWork
    {
        public CookingCompanionWorkKind Kind { get; private set; }

        public CookingTable? Table { get; private set; }

        public ItemId? Bowl { get; private set; }

        public int Elapsed { get; set; }

        public static CookingCompanionWork Idle() => new() { Kind = CookingCompanionWorkKind.Idle };

        public static CookingCompanionWork Inquiry(CookingTable table) =>
            new() { Kind = CookingCompanionWorkKind.Inquiring, Table = table };

        public static CookingCompanionWork Wash(ItemId bowl) =>
            new() { Kind = CookingCompanionWorkKind.Washing, Bowl = bowl };
    }
}

public sealed record CookingFrontOfHouseSchedule(
    int TableCount,
    int ServiceTicks,
    int ArrivalIntervalTicks,
    int InquiryTicks,
    int WashTicks,
    int DiningTicks,
    int WaitLimitTicks);

public sealed record CookingFrontOfHouseStep(int SeatedCount, bool Closing, bool CanSucceed, int UnsatisfiedCount);

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
