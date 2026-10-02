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
    private readonly List<CookingCustomer> _transit = new();
    private readonly Dictionary<string, CookingFrontWorkSnapshot> _jobs = new(StringComparer.Ordinal);
    private CookingFrontOfHouseFlow? _flow;
    private string? _manualPolicy;
    private Func<PlayerId, string, bool>? _canWork;
    private ICookingRecipeAuthorityGate? _frontAuthorityGate;
    internal void BindAuthorityGate(ICookingRecipeAuthorityGate gate) => _frontAuthorityGate = gate;
    public bool HasPlayerWork(PlayerId player) => _jobs.Values.Any(x => x.Player == player);
    private void EnsureFrontMutation()
    {
        if (_frontAuthorityGate is not null && !_frontAuthorityGate.IsAuthorityMutationOpen)
            throw new InvalidOperationException("Front-of-house writes require the owning level authority operation.");
    }

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
        _closing && SeatedCount == 0 && _transit.Count == 0 && _tables.All(x => !x.Dirty)
        && _work.Kind == CookingCompanionWorkKind.Idle && !_jobs.Values.Any(x => x.Player is not null);

    /// <summary>Preparation-only path input. Every edge is checked against the owner's immutable geometry.</summary>
    public void ConfigureFlow(CookingFrontOfHouseFlow flow)
    {
        EnsureFrontMutation();
        if (_nextGuest != 0 || _serviceTicks != 0) throw new InvalidOperationException("Flow cannot change during service.");
        _flow = FreezeFlow(flow, _schedule.TableCount);
    }

    /// <summary>The read-only predicate checks player existence, kitchen work exclusion, facing, and reach.</summary>
    public void ConfigureManualWork(string policyIdentity, Func<PlayerId, string, bool> canWork)
    {
        EnsureFrontMutation();
        if (string.IsNullOrWhiteSpace(policyIdentity)) throw new ArgumentException("Policy identity is required.");
        ArgumentNullException.ThrowIfNull(canWork);
        if (_manualPolicy is null && _serviceTicks != 0) throw new InvalidOperationException("Manual policy must be configured before service.");
        if (_manualPolicy is not null && _manualPolicy != policyIdentity) throw new InvalidOperationException("Manual policy cannot change.");
        _manualPolicy = policyIdentity;
        _canWork = canWork;
    }

    public CookingFrontWorkResult ClaimFrontWork(CookingRecipeSimulation kitchen, PlayerId player, string workId)
    {
        EnsureFrontMutation();
        if (_manualPolicy is null || _canWork is null || string.IsNullOrWhiteSpace(player.Value))
            return new(false, CookingFrontWorkRejection.PolicyMissing);
        if (!_jobs.TryGetValue(workId ?? "", out var job)) return new(false, CookingFrontWorkRejection.WorkMissing);
        if (job.Status is not (CookingFrontWorkStatus.Available or CookingFrontWorkStatus.Paused))
            return new(false, CookingFrontWorkRejection.Occupied);
        if (_jobs.Values.Any(x => x.Player == player)) return new(false, CookingFrontWorkRejection.PlayerBusy);
        if (!_canWork(player, job.Target)) return new(false, CookingFrontWorkRejection.OutOfReach);
        _jobs[job.Id] = job with { Player = player, Status = CookingFrontWorkStatus.Working, Companion = false };
        if (job.Kind == CookingCompanionWorkKind.Inquiring && FindTable(job.Target, job.Customer) is { Customer: { } customer })
            customer.Phase = CookingTablePhase.InquiryInProgress;
        return new(true, CookingFrontWorkRejection.None);
    }

    public CookingFrontWorkResult ContinueFrontWork(PlayerId player, string workId)
    {
        EnsureFrontMutation();
        if (!_jobs.TryGetValue(workId ?? "", out var job)) return new(false, CookingFrontWorkRejection.WorkMissing);
        if (job.Player != player) return new(false, CookingFrontWorkRejection.NotOwner);
        if (_canWork is null || !_canWork(player, job.Target)) return new(false, CookingFrontWorkRejection.OutOfReach);
        return new(true, CookingFrontWorkRejection.None);
    }

    public CookingFrontWorkResult StopFrontWork(PlayerId player, string workId)
    {
        EnsureFrontMutation();
        if (!_jobs.TryGetValue(workId ?? "", out var job)) return new(false, CookingFrontWorkRejection.WorkMissing);
        if (job.Player != player) return new(false, CookingFrontWorkRejection.NotOwner);
        PauseJob(job);
        return new(true, CookingFrontWorkRejection.None);
    }

    public CookingFrontOfHouseSnapshot Snapshot()
    {
        var customers = _tables
            .Where(table => table.Customer is not null)
            .OrderBy(table => table.Id, StringComparer.Ordinal)
            .Select(table => table.Customer!.Snapshot(table.Id))
            .Concat(_transit.OrderBy(x => x.ArrivalOrder).Select(x => x.Snapshot("")))
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
            _unsatisfied.ToArray())
        {
            Flow = _flow,
            ManualPolicyIdentity = _manualPolicy,
            Work = Array.AsReadOnly(_jobs.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray()),
            Tables = Array.AsReadOnly(_tables.Select(x => new CookingFrontTableSnapshot(x.Id,
                x.Dirty ? CookingFrontTableState.Dirty : x.Customer is null ? CookingFrontTableState.Free
                    : x.Customer.Phase == CookingTablePhase.WalkingToTable ? CookingFrontTableState.Reserved : CookingFrontTableState.Occupied,
                x.ClearSequence)).ToArray())
        };
    }

    public CookingFrontOfHouseCheckpoint ExportCheckpoint(OrderTemplateId activeOrderTemplate)
        => ExportCheckpoint(CookingFrontOfHouseMenu.Single(activeOrderTemplate));

    public CookingFrontOfHouseCheckpoint ExportCheckpoint(CookingFrontOfHouseMenu menu)
    {
        EnsureFrontMutation();
        BindMenu(menu);
        return new CookingFrontOfHouseCheckpoint(_orderMenu[0], Snapshot());
    }

    public CookingFrontOfHouseRestoreResult RestoreCheckpoint(
        CookingFrontOfHouseCheckpoint checkpoint,
        CookingRecipeSimulation kitchen)
    {
        EnsureFrontMutation();
        ArgumentNullException.ThrowIfNull(kitchen);
        if (checkpoint?.State is null)
            return CookingFrontOfHouseRestoreResult.Reject(CookingFrontOfHouseRestoreReason.CheckpointNull);
        if (!Equals(_schedule, checkpoint.State.Schedule))
            return CookingFrontOfHouseRestoreResult.Reject(CookingFrontOfHouseRestoreReason.ScheduleMismatch);
        if (checkpoint.State.Companion is null || _companion != checkpoint.State.Companion.Id)
            return CookingFrontOfHouseRestoreResult.Reject(CookingFrontOfHouseRestoreReason.CompanionMismatch);
        if (FlowCanonical(_flow) != FlowCanonical(checkpoint.State.Flow) || _manualPolicy != checkpoint.State.ManualPolicyIdentity)
            return CookingFrontOfHouseRestoreResult.Reject(CookingFrontOfHouseRestoreReason.ConfigurationMismatch);

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
        EnsureFrontMutation();
        ArgumentNullException.ThrowIfNull(kitchen);
        if (_manualPolicy is not null && _canWork is null)
            throw new InvalidOperationException("Restore requires rebinding the read-only manual-work policy before ticking.");
        BindMenu(menu);
        NoticeDirtyBowls(kitchen);
        EnsureJobs(kitchen);
        var readyJobs = _jobs.Keys.ToHashSet(StringComparer.Ordinal);
        AdvanceGuests(kitchen);
        EnsureJobs(kitchen);
        AdvanceManual(kitchen);
        if (_work.Kind != CookingCompanionWorkKind.Idle)
            AdvanceCompanion(kitchen);
        if (_work.Kind == CookingCompanionWorkKind.Idle)
            StartNextJob(kitchen, readyJobs);
        if (!_closing)
            TrySeatGuest();
        if (_flow is not null) SeatQueued();
        EnsureJobs(kitchen);
        if (!_closing && _serviceTicks >= _schedule.ServiceTicks)
            _closing = true;
        return new CookingFrontOfHouseStep(SeatedCount, _closing, CanSucceed, _unsatisfied.Count);
    }

    /// <summary>失败重开：丢掉座位、未满足、营业时钟和洗碗队列。不给新厨房开单，也不洗旧碗。</summary>
    public void DropFailedScene()
    {
        EnsureFrontMutation();
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
        _transit.Clear();
        _jobs.Clear();
    }

    /// <summary>下一小关：先收完正在做的询问或洗碗，再清掉座位、未满足和营业时钟。厨房里的脏碗保留。</summary>
    public void ResetForNextLevel(CookingRecipeSimulation kitchen, OrderTemplateId template)
        => ResetForNextLevel(kitchen, CookingFrontOfHouseMenu.Single(template));

    public void ResetForNextLevel(CookingRecipeSimulation kitchen, CookingFrontOfHouseMenu menu)
    {
        EnsureFrontMutation();
        if ((_flow is not null || _manualPolicy is not null) && !CanSucceed)
            throw new InvalidOperationException("The new front-of-house flow must naturally finish before a successful level reset.");
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
        _transit.Clear();
        _jobs.Clear();
    }

    /// <summary>成功收口前把做到一半的询问和洗碗按完成处理。</summary>
    public void FinishInProgress(CookingRecipeSimulation kitchen, OrderTemplateId template)
        => FinishInProgress(kitchen, CookingFrontOfHouseMenu.Single(template));

    public void FinishInProgress(CookingRecipeSimulation kitchen, CookingFrontOfHouseMenu menu)
    {
        EnsureFrontMutation();
        ArgumentNullException.ThrowIfNull(kitchen);
        BindMenu(menu);
        if (_flow is not null || _manualPolicy is not null) return;
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
        var activeId = CompanionJobId();
        if (activeId is not null && _jobs.TryGetValue(activeId, out var progress))
            _jobs[activeId] = progress with { ElapsedTicks = _work.Elapsed };
        if (_work.Elapsed < _work.RequiredTicks)
            return;

        if (_work.Kind == CookingCompanionWorkKind.Inquiring &&
            FindTable(_work.TargetTable, _work.TargetCustomer) is { } table)
        {
            CompleteInquiry(kitchen, table);
        }
        else if (_work.Kind == CookingCompanionWorkKind.Clearing && _tables.FirstOrDefault(x => x.Id == _work.TargetTable) is { } clearing)
        {
            CompleteJob(ClearId(clearing));
            clearing.Dirty = false;
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
        {
            if (_jobs.TryGetValue(InquiryId(customer.Id), out var failed))
                PauseJob(failed with { ElapsedTicks = Math.Min(failed.ElapsedTicks, failed.RequiredTicks - 1) });
            return;
        }

        customer.Phase = CookingTablePhase.Ordered;
        customer.Order = order;
        customer.OrderTemplate = template;
        customer.ElapsedTicks = 0;
        var jobId = InquiryId(customer.Id);
        var manual = _jobs.TryGetValue(jobId, out var job) && job.Player is not null;
        if (!manual) _completedCompanionTasks++;
        CompleteJob(jobId);
    }

    private void CompleteWash(CookingRecipeSimulation kitchen, ItemId bowl)
    {
        var washed = kitchen.CompleteWash(bowl);
        if (!washed.Accepted)
        {
            if (_jobs.TryGetValue(WashId(bowl), out var failed))
            {
                if (kitchen.DirtyBowlsAwaitingWash().Contains(bowl))
                    PauseJob(failed with { ElapsedTicks = Math.Min(failed.ElapsedTicks, failed.RequiredTicks - 1) });
                else _jobs[failed.Id] = failed with { Status = CookingFrontWorkStatus.Cancelled, Player = null, Companion = false };
            }
            if (kitchen.DirtyBowlsAwaitingWash().Contains(bowl)) _washQueue.Enqueue(bowl);
            else _queuedBowls.Remove(bowl);
            return;
        }

        _queuedBowls.Remove(bowl);
        RemoveWashQueueEntry(bowl);
        var jobId = WashId(bowl);
        var manual = _jobs.TryGetValue(jobId, out var job) && job.Player is not null;
        if (!manual) _completedCompanionTasks++;
        CompleteJob(jobId);
    }

    private void StartNextJob(CookingRecipeSimulation kitchen, IReadOnlySet<string> readyJobs)
    {
        var waiting = _tables
            .Where(table => table.Customer?.Phase == CookingTablePhase.WaitingForInquiry &&
                readyJobs.Contains(InquiryId(table.Customer.Id)) &&
                (!_jobs.TryGetValue(InquiryId(table.Customer.Id), out var job) || job.Player is null))
            .OrderBy(table => table.Customer!.ArrivalOrder)
            .FirstOrDefault();
        if (waiting?.Customer is { } customer)
        {
            customer.Phase = CookingTablePhase.InquiryInProgress;
            _work = CookingCompanionWork.Inquiry(customer.Id, waiting.Id, _schedule.InquiryTicks);
            StartCompanionJob(InquiryId(customer.Id));
            AdvanceCompanion(kitchen);
            return;
        }

        var dirtyTable = _tables.FirstOrDefault(x => x.Dirty && readyJobs.Contains(ClearId(x)) && (!_jobs.TryGetValue(ClearId(x), out var job) || job.Player is null));
        if (dirtyTable is not null)
        {
            _work = CookingCompanionWork.Clear(dirtyTable.Id, _flow!.ClearTableTicks);
            StartCompanionJob(ClearId(dirtyTable));
            AdvanceCompanion(kitchen);
            return;
        }

        while (_washQueue.Count > 0)
        {
            var bowl = _washQueue.Dequeue();
            if (_jobs.TryGetValue(WashId(bowl), out var manualJob) && manualJob.Player is not null)
            {
                _washQueue.Enqueue(bowl);
                return;
            }
            if (!kitchen.DirtyBowlsAwaitingWash().Contains(bowl))
            {
                _queuedBowls.Remove(bowl);
                continue;
            }

            _work = CookingCompanionWork.Wash(bowl, RequiredWashTicks());
            StartCompanionJob(WashId(bowl));
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
            if (customer.Phase == CookingTablePhase.WalkingToTable)
            {
                if (++customer.PathIndex >= _flow!.Tables.Single(x => x.TableId == table.Id).QueueToTable.Count - 1)
                {
                    customer.Phase = CookingTablePhase.WaitingForInquiry;
                    customer.PathIndex = 0;
                    customer.ElapsedTicks = 0;
                }
                continue;
            }
            if (customer.Phase is CookingTablePhase.InquiryInProgress &&
                customer.ElapsedTicks < _schedule.WaitLimitTicks)
            {
                continue;
            }
            if (customer.Phase == CookingTablePhase.Dining)
            {
                if (customer.ElapsedTicks >= _schedule.DiningTicks)
                    LeaveTable(table);
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
                    LeaveTable(table);
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
            LeaveTable(table);
        }
        AdvanceTransit();
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
        if (_flow is not null)
        {
            if (_transit.Count(x => x.Phase is CookingTablePhase.Arriving or CookingTablePhase.Queued) >= _flow.QueueCapacity) return;
            var arrival = ++_nextGuest;
            _transit.Add(new(new($"customer-{arrival}"), arrival, CookingTablePhase.Arriving, 0, null));
            return;
        }
        var table = _tables.FirstOrDefault(candidate => candidate.Customer is null);
        if (table is null)
            return;

        var sequence = ++_nextGuest;
        table.Customer = new CookingCustomer(new CookingCustomerId($"customer-{sequence}"), sequence,
            CookingTablePhase.WaitingForInquiry, 0, null);
    }

    private void NoticeDirtyBowls(CookingRecipeSimulation kitchen)
    {
        var dirty = kitchen.DirtyBowlsAwaitingWash().ToHashSet();
        foreach (var stale in _washQueue.Where(x => !dirty.Contains(x)).ToArray())
        { RemoveWashQueueEntry(stale); _queuedBowls.Remove(stale); }
        foreach (var bowl in dirty)
        {
            if (_queuedBowls.Add(bowl))
                _washQueue.Enqueue(bowl);
        }
    }

    private void RemoveWashQueueEntry(ItemId bowl)
    {
        var remaining = _washQueue.Where(x => x != bowl).ToArray();
        _washQueue.Clear();
        foreach (var queued in remaining) _washQueue.Enqueue(queued);
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
        if (_jobs.TryGetValue(InquiryId(customer), out var job) && job.Status != CookingFrontWorkStatus.Completed)
            _jobs[job.Id] = job with { Status = CookingFrontWorkStatus.Cancelled, Player = null, Companion = false };
    }

    private static string InquiryId(CookingCustomerId customer) => "inquiry:" + customer.Value;
    private static string WashCycleId(ItemId bowl, int cycle) => "wash:" + bowl.Value + (cycle == 1 ? "" : ":" + cycle.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private string WashId(ItemId bowl) => _jobs.Values.FirstOrDefault(x => x.Bowl == bowl
        && x.Status is not (CookingFrontWorkStatus.Completed or CookingFrontWorkStatus.Cancelled))?.Id ?? WashCycleId(bowl, 1);
    private static string ClearId(CookingTable table) => $"clear:{table.Id}:{table.ClearSequence}";

    private void EnsureJobs(CookingRecipeSimulation kitchen)
    {
        foreach (var table in _tables)
        {
            if (table.Customer is { Phase: CookingTablePhase.WaitingForInquiry } customer)
                _jobs.TryAdd(InquiryId(customer.Id), new(InquiryId(customer.Id), CookingCompanionWorkKind.Inquiring,
                    table.Id, customer.Id, null, 0, _schedule.InquiryTicks, CookingFrontWorkStatus.Available));
            if (table.Dirty)
                _jobs.TryAdd(ClearId(table), new(ClearId(table), CookingCompanionWorkKind.Clearing, table.Id, null, null,
                    0, _flow!.ClearTableTicks, CookingFrontWorkStatus.Available));
        }
        foreach (var bowl in kitchen.DirtyBowlsAwaitingWash())
        {
            var prior = _jobs.Values.Where(x => x.Bowl == bowl).ToArray();
            if (prior.Any(x => x.Status is not (CookingFrontWorkStatus.Completed or CookingFrontWorkStatus.Cancelled))) continue;
            var cycle = prior.Length == 0 ? 1 : prior.Max(x => x.Cycle) + 1;
            var id = WashCycleId(bowl, cycle);
            _jobs[id] = new(id, CookingCompanionWorkKind.Washing, "washing", null, bowl, 0,
                RequiredWashTicks(), CookingFrontWorkStatus.Available) { Cycle = cycle };
        }
    }

    private void PauseJob(CookingFrontWorkSnapshot job)
    {
        _jobs[job.Id] = job with { Player = null, Companion = false, Status = CookingFrontWorkStatus.Paused };
        if (job.Kind == CookingCompanionWorkKind.Inquiring && FindTable(job.Target, job.Customer) is { Customer: { } customer })
            customer.Phase = CookingTablePhase.WaitingForInquiry;
    }

    private void CompleteJob(string id)
    {
        if (_jobs.TryGetValue(id, out var job))
            _jobs[id] = job with { Status = CookingFrontWorkStatus.Completed, Player = null, Companion = false, ElapsedTicks = job.RequiredTicks };
    }

    private string? CompanionJobId() => _work.Kind switch
    {
        CookingCompanionWorkKind.Inquiring when _work.TargetCustomer is { } customer => InquiryId(customer),
        CookingCompanionWorkKind.Washing when _work.Bowl is { } bowl => WashId(bowl),
        CookingCompanionWorkKind.Clearing => _tables.Where(x => x.Id == _work.TargetTable).Select(ClearId).FirstOrDefault(),
        _ => null
    };

    private void StartCompanionJob(string id)
    {
        if (!_jobs.TryGetValue(id, out var job)) return;
        if (job.ElapsedTicks > 0) _work.RequiredTicks = job.RequiredTicks;
        _work.Elapsed = job.ElapsedTicks;
        _jobs[id] = job with { Status = CookingFrontWorkStatus.Working, Companion = true, RequiredTicks = _work.RequiredTicks };
    }

    private void AdvanceManual(CookingRecipeSimulation kitchen)
    {
        foreach (var job in _jobs.Values.Where(x => x.Player is not null).ToArray())
        {
            if (!_canWork!(job.Player!.Value, job.Target)) { PauseJob(job); continue; }
            if (job.Kind == CookingCompanionWorkKind.Washing && !kitchen.DirtyBowlsAwaitingWash().Contains(job.Bowl!.Value))
            {
                _jobs[job.Id] = job with { Status = CookingFrontWorkStatus.Cancelled, Player = null };
                _queuedBowls.Remove(job.Bowl.Value);
                RemoveWashQueueEntry(job.Bowl.Value);
                continue;
            }
            var progress = job with { ElapsedTicks = job.ElapsedTicks + 1 };
            _jobs[job.Id] = progress;
            if (progress.ElapsedTicks < progress.RequiredTicks) continue;
            if (job.Kind == CookingCompanionWorkKind.Inquiring && FindTable(job.Target, job.Customer) is { } table)
                CompleteInquiry(kitchen, table);
            else if (job.Kind == CookingCompanionWorkKind.Washing && job.Bowl is { } bowl)
                CompleteWash(kitchen, bowl);
            else if (job.Kind == CookingCompanionWorkKind.Clearing && _tables.FirstOrDefault(x => x.Id == job.Target) is { } clearing)
            {
                clearing.Dirty = false;
                CompleteJob(job.Id);
            }
        }
    }

    private void LeaveTable(CookingTable table)
    {
        var customer = table.Customer!;
        CancelInquiry(customer.Id);
        if (_flow is null) { table.Reset(); return; }
        customer.Phase = CookingTablePhase.Leaving;
        customer.ElapsedTicks = 0;
        customer.PathIndex = 0;
        customer.RouteTable = table.Id;
        _transit.Add(customer);
        table.Customer = null;
        table.Dirty = true;
        table.ClearSequence++;
    }

    private void AdvanceTransit()
    {
        if (_flow is null) return;
        foreach (var customer in _transit.ToArray())
        {
            customer.ElapsedTicks++;
            if (customer.Phase == CookingTablePhase.Arriving)
            {
                if (++customer.PathIndex >= _flow.EntranceToQueue.Count - 1)
                { customer.Phase = CookingTablePhase.Queued; customer.PathIndex = 0; customer.ElapsedTicks = 0; }
            }
            else if (customer.Phase == CookingTablePhase.Queued)
            {
                if (customer.ElapsedTicks >= _schedule.WaitLimitTicks)
                {
                    _unsatisfied.Add(CustomerOrder(customer.Id));
                    customer.Phase = CookingTablePhase.Leaving;
                    customer.PathIndex = 0;
                    customer.ElapsedTicks = 0;
                }
            }
            else if (customer.Phase == CookingTablePhase.Leaving)
            {
                var path = customer.RouteTable is null ? _flow.QueueToExit : _flow.Tables.Single(x => x.TableId == customer.RouteTable).TableToExit;
                if (++customer.PathIndex >= path.Count - 1) _transit.Remove(customer);
            }
        }
    }

    private void SeatQueued()
    {
        foreach (var customer in _transit.Where(x => x.Phase == CookingTablePhase.Queued).OrderBy(x => x.ArrivalOrder).ToArray())
        {
            var table = _tables.FirstOrDefault(x => x.Customer is null && !x.Dirty);
            if (table is null) break;
            _transit.Remove(customer);
            table.Customer = customer;
            customer.Phase = CookingTablePhase.WalkingToTable;
            customer.PathIndex = 0;
            customer.ElapsedTicks = 0;
        }
    }

    private static string? FlowCanonical(CookingFrontOfHouseFlow? flow) => flow is null ? null : JsonSerializer.Serialize(flow);

    private static CookingFrontOfHouseFlow FreezeFlow(CookingFrontOfHouseFlow flow, int tableCount)
    {
        ArgumentNullException.ThrowIfNull(flow);
        if (string.IsNullOrWhiteSpace(flow.GeometryIdentity) || flow.QueueCapacity < 1 || flow.ClearTableTicks < 1
            || flow.Walkable is null || flow.Walkable.Count > 65536 || flow.Tables is null || flow.Tables.Count != tableCount || flow.Tables.Any(x => x is null))
            throw new ArgumentException("Invalid front-of-house geometry.");
        var cells = flow.Walkable.ToHashSet();
        if (cells.Count != flow.Walkable.Count || cells.Count == 0) throw new ArgumentException("Invalid walkable cells.");
        if (flow.Spatial is null || flow.Spatial.InitialPoses is null || flow.Spatial.Anchors is null || flow.Spatial.Obstacles is null
            || flow.Spatial.Anchors.Any(x => x is null) || flow.Spatial.InitialPoses.Any(x => x is null)
            || flow.Spatial.Obstacles.Any(x => x is null || x.MinX >= x.MaxX || x.MinY >= x.MaxY))
            throw new ArgumentException("Spatial configuration is incomplete.");
        if (flow.CellSize <= 0 || flow.Spatial is null || flow.GeometryIdentity != CookingFrontOfHouseFlow.SpatialIdentity(flow.Spatial))
            throw new ArgumentException("Spatial geometry identity does not match.");
        (long X, long Y) Center(CookingFrontPoint point) => ((long)point.X * flow.CellSize + flow.CellSize / 2,
            (long)point.Y * flow.CellSize + flow.CellSize / 2);
        bool Clear(CookingFrontPoint a, CookingFrontPoint b)
        {
            if (flow.Spatial is not { } spatial) return true;
            var start = Center(a); var end = Center(b); var r = spatial.PlayerRadius;
            return r > 0 && start.X - r >= spatial.MinX && start.X + r <= spatial.MaxX
                && start.Y - r >= spatial.MinY && start.Y + r <= spatial.MaxY
                && end.X - r >= spatial.MinX && end.X + r <= spatial.MaxX && end.Y - r >= spatial.MinY && end.Y + r <= spatial.MaxY
                && spatial.Obstacles.All(o => !CookingSpatialConfiguration.SegmentHits(start.X, start.Y, end.X, end.Y,
                    (long)o.MinX - r, (long)o.MinY - r, (long)o.MaxX + r, (long)o.MaxY + r));
        }
        if (cells.Any(x => !Clear(x, x))) throw new ArgumentException("A walkable cell intersects the authority's geometry.");
        IReadOnlyList<CookingFrontPoint> Path(IReadOnlyList<CookingFrontPoint> path)
        {
            if (path is null || path.Count < 2 || path.Any(x => !cells.Contains(x))) throw new ArgumentException("Path leaves authoritative geometry.");
            for (var i = 1; i < path.Count; i++)
                if (Math.Abs((long)path[i].X - path[i - 1].X) + Math.Abs((long)path[i].Y - path[i - 1].Y) != 1 || !Clear(path[i - 1], path[i]))
                    throw new ArgumentException("A path edge must connect adjacent walkable cells.");
            return Array.AsReadOnly(path.ToArray());
        }
        var entrance = Path(flow.EntranceToQueue);
        var exit = Path(flow.QueueToExit);
        if (entrance[^1] != exit[0]) throw new ArgumentException("Queue endpoints do not match.");
        var tables = new List<CookingFrontTableRoute>();
        foreach (var table in flow.Tables.OrderBy(x => x.TableId, StringComparer.Ordinal))
        {
            var inbound = Path(table.QueueToTable);
            var outbound = Path(table.TableToExit);
            if (inbound[0] != entrance[^1] || inbound[^1] != outbound[0] || outbound[^1] != exit[^1])
                throw new ArgumentException("Table path endpoints do not match.");
            tables.Add(new(table.TableId, inbound, outbound));
        }
        if (!tables.Select(x => x.TableId).SequenceEqual(Enumerable.Range(1, tableCount).Select(x => $"table-{x}").OrderBy(x => x, StringComparer.Ordinal)))
            throw new ArgumentException("Table identities do not match schedule.");
        return flow with { EntranceToQueue = entrance, QueueToExit = exit, Tables = Array.AsReadOnly(tables.ToArray()),
            Walkable = Array.AsReadOnly(cells.OrderBy(x => x.X).ThenBy(x => x.Y).ToArray()), Spatial = flow.Spatial?.Freeze() };
    }

    /// <summary>Prevalidates an unpublished front state for a later owner-only generation commit.</summary>
    internal Action PrepareGenerationStateAdoption(CookingFrontOfHouse source)
    {
        EnsureFrontMutation();
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(this, source) || _schedule != source._schedule || _companion != source._companion ||
            FlowCanonical(_flow) != FlowCanonical(source._flow) || _manualPolicy != source._manualPolicy)
            throw new ArgumentException("The staged front state has a different configuration.");
        return () => CopyFrom(source);
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
        _flow = source._flow;
        _manualPolicy = source._manualPolicy;
        _transit.Clear();
        _transit.AddRange(source._transit.Select(x => x.Copy()));
        _jobs.Clear();
        foreach (var job in source._jobs) _jobs.Add(job.Key, job.Value);
    }

    private void Apply(CookingFrontOfHouseSnapshot state)
    {
        foreach (var table in _tables)
            table.Reset();
        foreach (var customer in state.Customers)
        {
            if (customer.TableId == "") { _transit.Add(CookingCustomer.From(customer)); continue; }
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
        _flow = state.Flow is null ? null : FreezeFlow(state.Flow, _schedule.TableCount);
        _manualPolicy = state.ManualPolicyIdentity;
        _jobs.Clear();
        foreach (var job in state.Work) _jobs.Add(job.Id, job);
        foreach (var tableState in state.Tables)
        {
            var table = _tables.Single(x => x.Id == tableState.Id);
            table.Dirty = tableState.State == CookingFrontTableState.Dirty;
            table.ClearSequence = tableState.ClearSequence;
        }
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
        if (state.Flow is null && state.Customers.Count > state.Schedule.TableCount)
            return CookingFrontOfHouseRestoreReason.TableInvalid;
        if (state.Flow is not null)
        {
            try { FreezeFlow(state.Flow, state.Schedule.TableCount); }
            catch (ArgumentException) { return CookingFrontOfHouseRestoreReason.GeometryInvalid; }
        }
        if (state.Work is null || state.Tables is null || state.Work.Any(x => x is null) || state.Tables.Any(x => x is null)
            || (state.ManualPolicyIdentity is not null && string.IsNullOrWhiteSpace(state.ManualPolicyIdentity)))
            return CookingFrontOfHouseRestoreReason.WorkInvalid;

        var expectedTables = Enumerable.Range(1, state.Schedule.TableCount)
            .Select(index => $"table-{index}")
            .ToHashSet(StringComparer.Ordinal);
        var tableIds = new HashSet<string>(StringComparer.Ordinal);
        var customerIds = new HashSet<CookingCustomerId>();
        var arrivalOrders = new HashSet<int>();
        foreach (var customer in state.Customers)
        {
            if (customer is null) return CookingFrontOfHouseRestoreReason.CustomerInvalid;
            var transit = customer.Phase is CookingTablePhase.Arriving or CookingTablePhase.Queued or CookingTablePhase.Leaving;
            if (transit ? state.Flow is null || customer.TableId != "" : !expectedTables.Contains(customer.TableId) || !tableIds.Add(customer.TableId))
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
                case CookingTablePhase.Arriving:
                case CookingTablePhase.Queued:
                case CookingTablePhase.WalkingToTable:
                    if (state.Flow is null || customer.Order is not null || customer.OrderTemplate is not null || customer.RouteTable is not null)
                        return CookingFrontOfHouseRestoreReason.CustomerInvalid;
                    var path = customer.Phase == CookingTablePhase.Arriving ? state.Flow.EntranceToQueue
                        : customer.Phase == CookingTablePhase.Queued ? null : state.Flow.Tables.Single(x => x.TableId == customer.TableId).QueueToTable;
                    if (customer.PathIndex < 0 || (path is null ? customer.PathIndex != 0 || customer.ElapsedTicks >= state.Schedule.WaitLimitTicks
                        : customer.PathIndex >= path.Count - 1 || customer.ElapsedTicks != customer.PathIndex)) return CookingFrontOfHouseRestoreReason.GeometryInvalid;
                    break;
                case CookingTablePhase.Leaving:
                    if (state.Flow is null || (customer.RouteTable is not null && !expectedTables.Contains(customer.RouteTable)))
                        return CookingFrontOfHouseRestoreReason.GeometryInvalid;
                    var leavePath = customer.RouteTable is null ? state.Flow.QueueToExit : state.Flow.Tables.Single(x => x.TableId == customer.RouteTable).TableToExit;
                    if (customer.PathIndex < 0 || customer.PathIndex >= leavePath.Count - 1 || customer.ElapsedTicks != customer.PathIndex)
                        return CookingFrontOfHouseRestoreReason.GeometryInvalid;
                    if (customer.Order is not null && (customer.RouteTable is null || customer.Order != expectedOrder || order is null
                        || customer.OrderTemplate != state.OrderMenu[(customer.ArrivalOrder - 1) % state.OrderMenu.Count]
                        || order.Template != customer.OrderTemplate
                        || order.Status is not (nameof(CookingOrderStatus.Completed) or nameof(CookingOrderStatus.Unsatisfied))))
                        return CookingFrontOfHouseRestoreReason.OrderInvalid;
                    break;
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
            if (customer.Phase is CookingTablePhase.WaitingForInquiry or CookingTablePhase.InquiryInProgress or CookingTablePhase.Ordered or CookingTablePhase.Dining
                && (customer.PathIndex != 0 || customer.RouteTable is not null)) return CookingFrontOfHouseRestoreReason.GeometryInvalid;
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
                    (companion.RequiredTicks != expectedWashTicks && !state.Work.Any(x => x.Companion && x.Kind == CookingCompanionWorkKind.Washing
                        && x.RequiredTicks == companion.RequiredTicks && x.RequiredTicks == state.Schedule.WashTicks)) || companion.ElapsedTicks < 1 ||
                    companion.ElapsedTicks >= companion.RequiredTicks ||
                    !kitchen.DirtyBowlsAwaitingWash().Contains(companion.Bowl.Value))
                {
                    return CookingFrontOfHouseRestoreReason.CompanionInvalid;
                }
                break;
            case CookingCompanionWorkKind.Clearing:
                if (state.Flow is null || companion.TargetCustomer is not null || companion.Bowl is not null
                    || companion.TargetTable is null || !state.Tables.Any(x => x.Id == companion.TargetTable && x.State == CookingFrontTableState.Dirty)
                    || companion.RequiredTicks != state.Flow.ClearTableTicks || companion.ElapsedTicks < 1 || companion.ElapsedTicks >= companion.RequiredTicks)
                    return CookingFrontOfHouseRestoreReason.CompanionInvalid;
                break;
            default:
                return CookingFrontOfHouseRestoreReason.CompanionInvalid;
        }

        if (state.Customers.Any(customer => customer.Phase == CookingTablePhase.InquiryInProgress &&
            customer.Id != companion.TargetCustomer && !state.Work.Any(x => x.Customer == customer.Id && x.Player is not null && x.Status == CookingFrontWorkStatus.Working)))
        {
            return CookingFrontOfHouseRestoreReason.CompanionInvalid;
        }

        var dirty = kitchen.DirtyBowlsAwaitingWash().ToHashSet();
        if (!ValidateExtendedState(state, expectedTables, dirty)) return CookingFrontOfHouseRestoreReason.WorkInvalid;
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
                state.Customers.Any(customer => customer.Id.Value == $"customer-{sequence}" && customer.Phase != CookingTablePhase.Leaving) ||
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

    private static bool ValidateExtendedState(CookingFrontOfHouseSnapshot state, HashSet<string> expectedTables, HashSet<ItemId> dirty)
    {
        var jobs = new HashSet<string>(StringComparer.Ordinal);
        var workers = new HashSet<PlayerId>();
        foreach (var job in state.Work)
        {
            if (job is null || string.IsNullOrWhiteSpace(job.Id) || !jobs.Add(job.Id) || !Enum.IsDefined(job.Status)
                || !Enum.IsDefined(job.Kind) || job.Kind == CookingCompanionWorkKind.Idle || job.RequiredTicks <= 0 || job.ElapsedTicks < 0
                || job.ElapsedTicks > job.RequiredTicks || (job.Status == CookingFrontWorkStatus.Completed ? job.ElapsedTicks != job.RequiredTicks
                    : job.Status != CookingFrontWorkStatus.Cancelled && job.ElapsedTicks >= job.RequiredTicks)
                || (job.Status == CookingFrontWorkStatus.Working ? (job.Player is null) == !job.Companion : job.Player is not null || job.Companion)
                || (job.Player is { } player && (state.ManualPolicyIdentity is null || string.IsNullOrWhiteSpace(player.Value) || !workers.Add(player)))) return false;
            var terminal = job.Status is CookingFrontWorkStatus.Completed or CookingFrontWorkStatus.Cancelled;
            if (job.Kind == CookingCompanionWorkKind.Inquiring)
            {
                if (job.Customer is null || job.Bowl is not null || job.Id != InquiryId(job.Customer.Value)
                    || !TryGetCustomerSequence(CustomerOrder(job.Customer.Value), out var sequence) || sequence > state.NextCustomerSequence
                    || !expectedTables.Contains(job.Target) || job.RequiredTicks != state.Schedule.InquiryTicks
                    || (!terminal && !state.Customers.Any(x => x.Id == job.Customer && x.TableId == job.Target
                        && x.Phase == (job.Status == CookingFrontWorkStatus.Working ? CookingTablePhase.InquiryInProgress : CookingTablePhase.WaitingForInquiry)))) return false;
            }
            else if (job.Kind == CookingCompanionWorkKind.Washing)
            {
                if (job.Bowl is null || job.Customer is not null || job.Cycle <= 0 || job.Id != WashCycleId(job.Bowl.Value, job.Cycle) || job.Target != "washing"
                    || (job.RequiredTicks != state.Schedule.WashTicks && job.RequiredTicks != RequiredWashTicks(state.Schedule, true))
                    || (!terminal && !dirty.Contains(job.Bowl.Value))) return false;
            }
            else if (state.Flow is null || job.Bowl is not null || job.Customer is not null || !expectedTables.Contains(job.Target)
                || job.RequiredTicks != state.Flow.ClearTableTicks || !job.Id.StartsWith("clear:" + job.Target + ":", StringComparison.Ordinal)
                || (!terminal && !state.Tables.Any(x => x.Id == job.Target && x.State == CookingFrontTableState.Dirty
                    && job.Id == $"clear:{x.Id}:{x.ClearSequence}"))) return false;
            if (job.Kind != CookingCompanionWorkKind.Washing && job.Cycle != 1) return false;
            if (job.Kind == CookingCompanionWorkKind.Clearing)
            {
                var suffix = job.Id.AsSpan(("clear:" + job.Target + ":").Length);
                if (!int.TryParse(suffix, out var clearSequence) || clearSequence <= 0
                    || job.Id != $"clear:{job.Target}:{clearSequence}"
                    || !state.Tables.Any(x => x.Id == job.Target && x.ClearSequence >= clearSequence)) return false;
            }
            var companion = state.Companion;
            if (job.Companion && (job.Kind != companion.Work || job.ElapsedTicks != companion.ElapsedTicks
                || job.RequiredTicks != companion.RequiredTicks || job.Customer != companion.TargetCustomer
                || job.Bowl != companion.Bowl || (job.Kind != CookingCompanionWorkKind.Washing && job.Target != companion.TargetTable))) return false;
        }
        if (state.Work.Count(x => x.Companion) != (state.Companion.Work == CookingCompanionWorkKind.Idle ? 0 : 1)) return false;
        foreach (var cycles in state.Work.Where(x => x.Kind == CookingCompanionWorkKind.Washing).GroupBy(x => x.Bowl))
        {
            var latest = cycles.Max(x => x.Cycle);
            var unfinished = cycles.Where(x => x.Status is not (CookingFrontWorkStatus.Completed or CookingFrontWorkStatus.Cancelled)).ToArray();
            if (latest != cycles.Count() || cycles.Select(x => x.Cycle).Distinct().Count() != cycles.Count()
                || unfinished.Length > 1 || unfinished.Any(x => x.Cycle != latest)) return false;
        }
        var tableStates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in state.Tables)
        {
            if (table is null || !expectedTables.Contains(table.Id) || !tableStates.Add(table.Id) || !Enum.IsDefined(table.State)
                || table.ClearSequence < 0 || table.ClearSequence > state.NextCustomerSequence || (state.Flow is null && table.State is CookingFrontTableState.Reserved or CookingFrontTableState.Dirty)) return false;
            var guest = state.Customers.SingleOrDefault(x => x.TableId == table.Id);
            var expected = guest is null ? table.State == CookingFrontTableState.Dirty ? CookingFrontTableState.Dirty : CookingFrontTableState.Free
                : guest.Phase == CookingTablePhase.WalkingToTable ? CookingFrontTableState.Reserved : CookingFrontTableState.Occupied;
            if (table.State != expected || (table.State == CookingFrontTableState.Dirty && table.ClearSequence == 0)) return false;
        }
        if (state.Tables.Count != state.Schedule.TableCount) return false;
        if (state.Flow is not null && state.Customers.Count(x => x.Phase is CookingTablePhase.Arriving or CookingTablePhase.Queued) > state.Flow.QueueCapacity) return false;
        return true;
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
        public bool Dirty { get; set; }
        public int ClearSequence { get; set; }

        public void Reset() { Customer = null; Dirty = false; ClearSequence = 0; }

        public CookingTable Copy() => new(Id) { Customer = Customer?.Copy(), Dirty = Dirty, ClearSequence = ClearSequence };
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
        public int PathIndex { get; set; }
        public string? RouteTable { get; set; }

        public CookingCustomerSnapshot Snapshot(string tableId) =>
            new(Id, tableId, ArrivalOrder, Phase, ElapsedTicks, Order, OrderTemplate) { PathIndex = PathIndex, RouteTable = RouteTable };

        public CookingCustomer Copy() => new(Id, ArrivalOrder, Phase, ElapsedTicks, Order, OrderTemplate) { PathIndex = PathIndex, RouteTable = RouteTable };

        public static CookingCustomer From(CookingCustomerSnapshot snapshot) =>
            new(snapshot.Id, snapshot.ArrivalOrder, snapshot.Phase, snapshot.ElapsedTicks, snapshot.Order,
                snapshot.OrderTemplate) { PathIndex = snapshot.PathIndex, RouteTable = snapshot.RouteTable };
    }

    private struct CookingCompanionWork
    {
        public CookingCompanionWorkKind Kind { get; private set; }
        public CookingCustomerId? TargetCustomer { get; private set; }
        public string? TargetTable { get; private set; }
        public ItemId? Bowl { get; private set; }
        public int Elapsed { get; set; }
        public int RequiredTicks { get; set; }

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
        public static CookingCompanionWork Clear(string table, int requiredTicks) =>
            new() { Kind = CookingCompanionWorkKind.Clearing, TargetTable = table, RequiredTicks = requiredTicks };

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

public enum CookingFrontDeliveryMode { ServingAnchor, CustomerTable }

/// <summary>Content-owned service destination. Customer tables come from the current order owner.</summary>
public sealed record CookingFrontDeliveryPolicy(CookingFrontDeliveryMode Mode, string? ServingAnchor = null);

/// <summary>Trusted application configuration; it is provided by the existing gameplay factory, never inferred from a checkpoint.</summary>
public sealed record CookingFrontOfHouseConfiguration(CookingFrontOfHouseSchedule Schedule,
    IReadOnlyList<OrderTemplateId> Menu, CookingFrontOfHouseFlow? Flow = null,
    string? ManualPolicyIdentity = null, string WashingAnchor = "washing",
    string EntranceAnchor = "customer-entrance", string QueueAnchor = "queue", string ExitAnchor = "exit",
    CookingFrontDeliveryPolicy? DeliveryPolicy = null)
{
    public CookingFrontOfHouseConfiguration Freeze()
    {
        var house = new CookingFrontOfHouse(Schedule);
        var menu = new CookingFrontOfHouseMenu(Menu);
        if (Flow is not null) house.ConfigureFlow(Flow);
        if ((ManualPolicyIdentity is not null && string.IsNullOrWhiteSpace(ManualPolicyIdentity)) || string.IsNullOrWhiteSpace(WashingAnchor)
            || string.IsNullOrWhiteSpace(EntranceAnchor) || string.IsNullOrWhiteSpace(QueueAnchor) || string.IsNullOrWhiteSpace(ExitAnchor))
            throw new ArgumentException("Front work policy and washing anchor must be valid.");
        if (DeliveryPolicy is { } delivery && (!Enum.IsDefined(delivery.Mode) ||
            (delivery.Mode == CookingFrontDeliveryMode.ServingAnchor ? string.IsNullOrWhiteSpace(delivery.ServingAnchor) : delivery.ServingAnchor is not null)))
            throw new ArgumentException("Delivery mode and service anchor must be valid.");
        return this with { Menu = Array.AsReadOnly(menu.Templates.ToArray()), Flow = house.Snapshot().Flow };
    }
    public string CanonicalText() => JsonSerializer.Serialize(Freeze());
    public string Identity() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));
}

public sealed record CookingFrontOfHouseStep(int SeatedCount, bool Closing, bool CanSucceed, int UnsatisfiedCount);

public readonly record struct CookingFrontPoint(int X, int Y);
public sealed record CookingFrontTableRoute(string TableId, IReadOnlyList<CookingFrontPoint> QueueToTable, IReadOnlyList<CookingFrontPoint> TableToExit);
/// <summary>Walkable cells must be adapted from the same validated spatial configuration used by the kitchen.</summary>
public sealed record CookingFrontOfHouseFlow(string GeometryIdentity, IReadOnlyList<CookingFrontPoint> Walkable,
    IReadOnlyList<CookingFrontPoint> EntranceToQueue, IReadOnlyList<CookingFrontPoint> QueueToExit,
    IReadOnlyList<CookingFrontTableRoute> Tables, int QueueCapacity, int ClearTableTicks)
{
    public CookingSpatialConfiguration? Spatial { get; init; }
    public int CellSize { get; init; } = 1000;
    public static string SpatialIdentity(CookingSpatialConfiguration spatial) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(spatial with
        {
            Anchors = spatial.Anchors.OrderBy(x => x.Kind).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            InitialPoses = spatial.InitialPoses.OrderBy(x => x.Player.Value, StringComparer.Ordinal).ToArray(),
            Obstacles = spatial.Obstacles.OrderBy(x => x.MinX).ThenBy(x => x.MinY).ThenBy(x => x.MaxX).ThenBy(x => x.MaxY).ToArray()
        }))));
}
public enum CookingFrontTableState { Free, Reserved, Occupied, Dirty }
public sealed record CookingFrontTableSnapshot(string Id, CookingFrontTableState State, int ClearSequence);
public enum CookingFrontWorkStatus { Available, Working, Paused, Completed, Cancelled }
public enum CookingFrontWorkRejection { None, PolicyMissing, WorkMissing, Occupied, PlayerBusy, OutOfReach, NotOwner }
public sealed record CookingFrontWorkResult(bool Accepted, CookingFrontWorkRejection Reason);
public sealed record CookingFrontWorkSnapshot(string Id, CookingCompanionWorkKind Kind, string Target,
    CookingCustomerId? Customer, ItemId? Bowl, int ElapsedTicks, int RequiredTicks, CookingFrontWorkStatus Status,
    PlayerId? Player = null, bool Companion = false)
{
    public int Cycle { get; init; } = 1;
}

public sealed record CookingCustomerSnapshot(
    CookingCustomerId Id,
    string TableId,
    int ArrivalOrder,
    CookingTablePhase Phase,
    int ElapsedTicks,
    OrderId? Order,
    OrderTemplateId? OrderTemplate = null)
{
    [System.Text.Json.Serialization.JsonRequired] public int PathIndex { get; init; }
    [System.Text.Json.Serialization.JsonRequired] public string? RouteTable { get; init; }
}

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
    [System.Text.Json.Serialization.JsonRequired] public CookingFrontOfHouseFlow? Flow { get; init; }
    [System.Text.Json.Serialization.JsonRequired] public string? ManualPolicyIdentity { get; init; }
    [System.Text.Json.Serialization.JsonRequired] public IReadOnlyList<CookingFrontWorkSnapshot> Work { get; init; } = Array.Empty<CookingFrontWorkSnapshot>();
    [System.Text.Json.Serialization.JsonRequired] public IReadOnlyList<CookingFrontTableSnapshot> Tables { get; init; } = Array.Empty<CookingFrontTableSnapshot>();
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string CanonicalText() => JsonSerializer.Serialize(new { Legacy = new CanonicalFrontOfHouse(
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
        UnsatisfiedOrders.Select(order => order.Value).ToArray()), Flow, ManualPolicyIdentity,
        Work = Work.OrderBy(x => x.Id, StringComparer.Ordinal), Tables = Tables.OrderBy(x => x.Id, StringComparer.Ordinal),
        Routes = Customers.OrderBy(x => x.Id.Value, StringComparer.Ordinal).Select(x => new { x.Id, x.PathIndex, x.RouteTable }) }, CanonicalJsonOptions);

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
    ConfigurationMismatch,
    GeometryInvalid,
    WorkInvalid,
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
    Arriving,
    Queued,
    WalkingToTable,
    Leaving,
}

public enum CookingCompanionWorkKind
{
    Idle,
    Inquiring,
    Washing,
    Clearing,
}
