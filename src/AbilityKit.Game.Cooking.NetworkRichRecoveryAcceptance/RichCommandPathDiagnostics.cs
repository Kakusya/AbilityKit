using System.Diagnostics;
using AbilityKit.Game.Cooking.RichEvidence;
using AbilityKit.Game.Cooking.Session;

namespace AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;

// Application observations only: this object is never constructed when diagnostics are OFF.
internal sealed class RichCommandPathDiagnostics(RichDiagnosticOptions options)
{
    private readonly object _gate = new();
    private readonly Ring<RichDiagnosticWait> _waits = new(64);
    private readonly Ring<RichDiagnosticCallback> _callbacks = new(256);
    private readonly Ring<RichDiagnosticFrame> _frames = new(64);
    private readonly Ring<RichDiagnosticSnapshot> _snapshots = new(8);
    private long _ordinal, _callbackOrdinal;
    private bool _active;
    private int? _framesAfterTerminal;
    private readonly HashSet<string> _snapshotMilestones = new(StringComparer.Ordinal);
    private readonly Stack<RichDiagnosticWait> _waitStack = new();
    private RichDiagnosticWait? _current;
    private RichDiagnosticText? _failure;
    public string? SelectedCorrelation { get; private set; }
    public string? SelectedDomain { get; private set; }
    public bool Active { get { lock (_gate) return _active; } }
    public static long Now => Stopwatch.GetTimestamp();
    public static RichCommandPathDiagnostics? Create(RichDiagnosticOptions? options) => options?.Enabled == true ? new(options) : null;

    public static RichDiagnosticOptions Parse(string[] args)
    {
        string? Read(string key)
        {
            var indices = args.Select((value, index) => (value, index)).Where(x => x.value == key).ToArray();
            if (indices.Length > 1) throw new ArgumentException("Duplicate diagnostic option: " + key);
            if (indices.Length == 0) return null;
            var index = indices[0].index;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Missing diagnostic option value: " + key);
            return args[index + 1];
        }
        foreach (var arg in args.Where(x => x.StartsWith("--diagnostic", StringComparison.Ordinal)))
            if (arg is not ("--diagnostics" or "--diagnostic-participant" or "--diagnostic-stable"))
                throw new ArgumentException("Unsupported diagnostic option: " + arg);
        var mode = Read("--diagnostics") ?? "OFF";
        if (mode is not ("OFF" or "ON")) throw new ArgumentException("Diagnostics must be OFF or ON.");
        var participant = Read("--diagnostic-participant"); var stable = Read("--diagnostic-stable");
        if (mode == "OFF") {
            if (participant is not null || stable is not null) throw new ArgumentException("OFF cannot have a diagnostic selector.");
            return new(false, null, null);
        }
        static bool Identifier(string? value) => value is { Length: > 0 and <= 128 } &&
            value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
        if (!Identifier(participant) || !Identifier(stable)) throw new ArgumentException("ON requires two bounded diagnostic identifiers.");
        return new(true, participant, stable);
    }

    public void ObserveCommand(CookingNetworkWireCommand wire, string correlation)
    {
        if (wire.Command.Player.Value != options.Participant || wire.StableCommandId != options.Stable) return;
        lock (_gate) {
            SelectedCorrelation = correlation;
            SelectedDomain = wire.Scope is { } scope ? CookingNetworkWireCodec.DomainId(wire.ServerSessionInstance, scope, wire.Command.Player, wire.StableCommandId).Value : null;
            _active = true;
        }
    }
    public long CallbackId() { lock (_gate) return ++_callbackOrdinal; }
    public void Callback(RichDiagnosticCallback row)
    {
        lock (_gate) if (_active || row.Failure is not null) _callbacks.Add(++_ordinal, row with { Ordinal = _ordinal });
    }
    public void Terminal(string correlation)
    {
        lock (_gate) if (correlation == SelectedCorrelation) _framesAfterTerminal = 8;
    }
    public void Projection(string correlation) { lock (_gate) if (correlation == SelectedCorrelation) _active = false; }
    public void Frame(RichDiagnosticFrame row)
    {
        lock (_gate) {
            if (!_active) return;
            _frames.Add(++_ordinal, row with { Ordinal = _ordinal });
            if (_framesAfterTerminal is { } left) {
                _framesAfterTerminal = left - 1;
                if (left <= 1) _active = false;
            }
        }
    }
    public bool ReserveSnapshot(string milestone)
    {
        lock (_gate) {
            if (SelectedCorrelation is null || _snapshotMilestones.Count >= 8 || !_snapshotMilestones.Add(milestone)) return false;
            return true;
        }
    }
    public void Snapshot(RichDiagnosticSnapshot row) { lock (_gate) _snapshots.Add(++_ordinal, row with { Ordinal = _ordinal }); }
    public long BeginWait(string stage, string participant, long until, long whole, string? correlation,
        RichCallerCommand? command, long? sendBefore, long? sendAfter, long? resultStart, long? projectionStart,
        long? target, RichPeerDiagnosticState? peer)
    {
        lock (_gate) {
            var now = Environment.TickCount64;
            var wire = command?.Wire;
            var domain = wire?.Scope is { } scope ? CookingNetworkWireCodec.DomainId(wire.ServerSessionInstance, scope, wire.Command.Player, wire.StableCommandId).Value : null;
            var id = ++_ordinal;
            _current = new(id, _waitStack.TryPeek(out var parent) ? parent.Ordinal : null, stage, correlation,
                wire?.StableCommandId, domain, wire?.Command.Operation.ToString(), participant, command?.SentOrdinal,
                sendBefore, sendAfter, resultStart, projectionStart, Now, Now, until, whole,
                Math.Max(0, until - now), Math.Max(0, whole - now), null, "ACTIVE", target, peer);
            _waitStack.Push(_current); return id;
        }
    }
    public void EndWait(long id, string status, bool? predicate, long? target, RichPeerDiagnosticState? peer)
    {
        lock (_gate) {
            if (!_waitStack.TryPeek(out var active) || active.Ordinal != id) { _failure = Text("Wait stack identity mismatch."); return; }
            _waitStack.Pop(); var now = Environment.TickCount64;
            var finished = active with { Observed = Now, RemainingOperationMs = Math.Max(0, active.OperationDeadline - now),
                RemainingWholeMs = Math.Max(0, active.WholeDeadline - now), PredicateResult = predicate,
                Status = status, TargetResultVersion = target, Peer = peer };
            _waits.Add(id, finished); _current = _waitStack.TryPeek(out var parent) ? parent : finished;
        }
    }
    public static RichDiagnosticText Text(string value) => new(value.Length <= 4096 ? value : value[..4096], value.Length, value.Length > 4096);
    public void Failure(Exception error) { lock (_gate) _failure = Text(error.ToString()); }
    public RichCommandPathReport Export()
    {
        lock (_gate) return new(1, options, Stopwatch.Frequency, _current, _waits.Export(), _callbacks.Export(), _frames.Export(),
            _snapshots.Export(), "UNOBSERVABLE_PRIVATE", "UNOBSERVABLE_PRIVATE", "UNOBSERVABLE_PRIVATE",
            "UNKNOWN: application callbacks are not native socket/fragment/wake timestamps.",
            "Opt-in application observation overhead; no overhead subtraction. Diagnostics getter copies retained timings at <=8 milestones. Wait IDs identify entry; nested completion order differs.", _failure,
            _snapshotMilestones.Count, "Operation/whole deadlines and remaining durations: Environment.TickCount64 milliseconds. Other timestamps: Stopwatch ticks.");
    }
    internal sealed class Ring<T>(int capacity)
    {
        private readonly Queue<(long Ordinal, T Row)> _rows = new(); private long _seen;
        public void Add(long ordinal, T row) { _seen++; if (_rows.Count == capacity) _rows.Dequeue(); _rows.Enqueue((ordinal, row)); }
        public RichDiagnosticRing<T> Export() => new(capacity, _seen, _rows.Count, _seen - _rows.Count,
            _rows.TryPeek(out var first) ? first.Ordinal : null, _rows.Count == 0 ? null : _rows.Last().Ordinal, _rows.Select(x => x.Row).ToArray());
    }
    // Mirrors byte consumption already reported by NetworkFrameReader, without decoding a second time.
    internal sealed class CallbackSegments
    {
        private readonly Queue<(long Id, int Bytes)> _segments = new(); private (long Id, int Bytes)? _head;
        private bool _overflow;
        public void Append(long id, int bytes)
        {
            if (_overflow || bytes == 0) return;
            if (_segments.Count >= 256) { _segments.Clear(); _head = null; _overflow = true; return; }
            _segments.Enqueue((id, bytes));
        }
        public string Consume(int bytes)
        {
            if (_overflow) return "Callback mapping truncated at256 segments; native first fragment UNKNOWN.";
            long? first = null, last = null;
            while (bytes > 0) {
                if (_head is null) {
                    if (!_segments.TryDequeue(out var next)) return "Callback byte mapping unavailable; native first fragment UNKNOWN.";
                    _head = next;
                }
                var head = _head.Value; first ??= head.Id; last = head.Id;
                var used = Math.Min(bytes, head.Bytes); bytes -= used;
                _head = used == head.Bytes ? null : (head.Id, head.Bytes - used);
            }
            return "Framework callback range " + first + ".." + last + "; native first fragment UNKNOWN.";
        }
    }
}
