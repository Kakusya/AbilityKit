using System.Text.Json;
using System.Threading.Channels;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

// Owner publication never awaits file I/O. Only DrainAsync writes events.jsonl.
public sealed class FlowEventCollector : IFlowEventSink
{
    private readonly RunIdentity run;
    private readonly FlowLogLimits limits;
    private readonly Channel<(LoggedEvent Event, byte[] Bytes)> queue;
    private readonly Dictionary<string, long> hostSequences = new(StringComparer.Ordinal);
    private readonly List<LoggedEvent> window = new();
    private readonly Dictionary<string, CommandObservation> commands = new(StringComparer.Ordinal);
    private readonly List<FlowObservation> observations = new();
    private readonly object gate = new();
    private readonly Task writer;
    private readonly Func<Stream> openStream;
    private readonly CancellationTokenSource writerCancellation = new();
    private long bytes, accepted, written;
    private bool incomplete, closed;
    private string? error;
    public bool EvidenceComplete { get { lock (gate) return !incomplete; } }
    public string? ErrorCode { get { lock (gate) return error; } }
    public IReadOnlyList<LoggedEvent> EventWindow { get { lock (gate) return window.ToArray(); } }

    public FlowEventCollector(string path, RunIdentity run, FlowLogLimits limits, Func<Stream>? openStream = null)
    {
        this.run = run; this.limits = limits;
        if (limits.QueueCapacity is < 1 or > 256 || limits.MaxEventBytes is < 1 or > 65536 || limits.MaxEvents is < 1 or > 4096 ||
            limits.MaxEventLogBytes is < 1 or > 8388608) throw new ArgumentException("Invalid collector limits.");
        this.openStream = openStream ?? (() => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            4096, FileOptions.Asynchronous));
        queue = Channel.CreateBounded<(LoggedEvent, byte[])>(new BoundedChannelOptions(limits.QueueCapacity)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });
        writer = Task.Run(DrainAsync); // File-only worker; never sees or calls an ET host.
    }

    public bool TryPublish(FlowEvent value)
    {
        lock (gate)
        {
            if (incomplete) return false;
            if (closed || value.SchemaVersion != 1 || value.RunId != run.RunId || value.RunGeneration != run.RunGeneration ||
                string.IsNullOrWhiteSpace(value.HostId) || value.HostSequence != hostSequences.GetValueOrDefault(value.HostId) + 1 ||
                !Enum.IsDefined(value.Kind) || value.Kind == FlowEventKind.CommandObserved && (value.Command is null || value.CallId != value.Command.CallId) ||
                value.Kind is FlowEventKind.StateObserved or FlowEventKind.RoleReady && value.Observation is null ||
                value.Kind == FlowEventKind.RuleChecked && value.Check is null)
                return Fail("InvalidEventIdentitySequenceOrPayload");
            var logged = new LoggedEvent(accepted + 1, DateTimeOffset.UtcNow, value);
            byte[] encoded;
            try { encoded = JsonSerializer.SerializeToUtf8Bytes(logged, FlowJson.Options); }
            catch (Exception) { return Fail("EventSerializationFailure"); }
            if (encoded.Length + 1 > limits.MaxEventBytes || accepted >= limits.MaxEvents || bytes + encoded.Length + 1 > limits.MaxEventLogBytes)
                return Fail("EventLimitExceeded");
            if (!queue.Writer.TryWrite((logged, encoded))) return Fail("CollectorQueueOverflow");
            hostSequences[value.HostId] = value.HostSequence;
            accepted++; bytes += encoded.Length + 1;
            window.Add(logged);
            while (window.Count > 16) window.RemoveAt(0);
            if (value.Command is { } command && !commands.TryAdd(command.CallId, command)) return Fail("DuplicateCommandObservation");
            if (value.Observation is { } observation) observations.Add(observation);
            return true;
        }
    }

    public bool HasRequiredEvents(FlowEvidence evidence)
    {
        lock (gate) return !incomplete && evidence.Commands.All(c => commands.TryGetValue(c.CallId, out var logged) && logged == c) &&
            evidence.Cuts.Values.All(c => observations.Contains(c));
    }

    public async Task CompleteAsync(CancellationToken token)
    {
        lock (gate) { closed = true; queue.Writer.TryComplete(); }
        try { await writer.WaitAsync(token); }
        catch (OperationCanceledException)
        {
            lock (gate) Fail("CollectorFlushTimeout");
            writerCancellation.Cancel();
            await writer;
            throw;
        }
        lock (gate) if (written != accepted) Fail("MissingWrittenEvents");
    }

    private bool Fail(string code) { incomplete = true; error ??= code; return false; }

    private async Task DrainAsync()
    {
        try
        {
            await using var stream = openStream();
            await foreach (var item in queue.Reader.ReadAllAsync(writerCancellation.Token).ConfigureAwait(false))
            {
                await stream.WriteAsync(item.Bytes, writerCancellation.Token).ConfigureAwait(false);
                await stream.WriteAsync(new byte[] { (byte)'\n' }, writerCancellation.Token).ConfigureAwait(false);
                lock (gate)
                {
                    written++;
                }
            }
            await stream.FlushAsync(writerCancellation.Token).ConfigureAwait(false);
            if (stream.CanSeek && stream.Length != bytes) lock (gate) Fail("PartialEventWrite");
        }
        catch (Exception e) { lock (gate) Fail("EventWriteFailure:" + e.GetType().Name); }
    }
}
